using System;
using System.IO;
using Cosmos.HAL.Audio;
using Cosmos.System.Audio;
using Cosmos.System.Audio.IO;
using Cosmos.System.Audio.DSP.Processing;

// اگر پروژه‌ی شما نسخه‌ی قدیمی‌تر Cosmos را استفاده می‌کند و AC97 در این
// namespace پیدا نشد، این using را با یکی از این دو جایگزین کنید:
//   using Cosmos.HAL.Drivers.PCI.Audio;   (نسخه‌های جدیدتر)
//   using Cosmos.HAL.Drivers.Audio;       (نسخه‌های قدیمی‌تر)
using Cosmos.HAL.Drivers.PCI.Audio;

namespace ParsOS.Audio
{
    /// <summary>
    /// درایور صدا و صوت ParsOS.
    /// روی زیرساخت CAI (Cosmos Audio Infrastructure) و کارت صدای AC97 سوار می‌شود.
    /// پشتیبانی می‌کند از:
    ///   - WAV  (PCM آنکد نشده / uncompressed) → مستقیماً توسط Cosmos پارس می‌شود
    ///   - MP3  (MPEG-1 Layer III)             → با دیکودر داخلی Mp3Decoder
    ///
    /// نحوه‌ی استفاده (فعلاً در جایی صدا زده نمی‌شود، طبق درخواست):
    ///   SoundDriver.Initialize();
    ///   SoundDriver.PlayFile(@"0:\Music\song.mp3");
    ///   ...
    ///   SoundDriver.Stop();
    /// </summary>
    public static class SoundDriver
    {
        private static AC97 _ac97;
        private static AudioMixer _mixer;
        private static AudioManager _manager;
        private static GainPostProcessor _gain;
        private static AudioStream _currentStream;
        private static SeekableAudioStream _seekableStream; // reference تایپ‌شده برای Position/Length

        // چون SeekableAudioStream پراپرتی Format ندارد، اطلاعات فرمت جریان
        // فعلی (برای محاسبه‌ی زمان سپری‌شده/کل) را خودمان نگه می‌داریم.
        private static int _currentSampleRate;
        private static int _currentChannels;
        private static int _currentBytesPerSample = 2; // 16-bit PCM = 2 بایت

        public static bool IsInitialized { get; private set; }
        public static bool IsPlaying { get; private set; }
        public static bool IsPaused { get; private set; }
        public static string LastError { get; private set; } = "";

        // مقدار ولوم فعلی (0.0 تا 1.0) — چون GainPostProcessor.Gain معمولاً
        // فقط قابل نوشتن است (setter-only از دید بیرون)، مقدارش را خودمان
        // نگه می‌داریم تا UI بتواند آن را بخواند (SoundDriver.CurrentVolume).
        private static float _currentVolume = 1.0f;
        public static float CurrentVolume => _currentVolume;

        // ─────────────────────────────────────────────────────────────
        //  راه‌اندازی کارت صدا
        // ─────────────────────────────────────────────────────────────
        public static bool Initialize(int bufferSize = 4096)
        {
            if (IsInitialized) return true;

            try
            {
                _ac97 = AC97.Initialize((ushort)bufferSize);

                _mixer = new AudioMixer();
                _gain = new GainPostProcessor { Gain = 1.0f };
                _mixer.PostProcessors.Add(_gain);

                _manager = new AudioManager
                {
                    Stream = _mixer,
                    Output = _ac97
                };
                _manager.Enable();

                IsInitialized = true;
                Console.WriteLine("[SoundDriver] AC97 initialized OK. bufferSize=" + bufferSize);
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                IsInitialized = false;
                Console.WriteLine("[SoundDriver] Init FAILED: " + e.Message);
                Console.WriteLine("[SoundDriver] (کارت صدای AC97 پیدا نشد یا محیط مجازی‌ساز آن را ارائه نمی‌دهد)");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  پخش بر اساس پسوند فایل — روی دیسک (VFS)
        // ─────────────────────────────────────────────────────────────
        public static bool PlayFile(string path)
        {
            if (!IsInitialized && !Initialize())
                return false;

            if (!File.Exists(path))
            {
                LastError = "File not found: " + path;
                Console.WriteLine("[SoundDriver] " + LastError);
                return false;
            }

            try
            {
                byte[] data = File.ReadAllBytes(path);
                string ext = Path.GetExtension(path).ToLowerInvariant();

                switch (ext)
                {
                    case ".wav":
                        return PlayWav(data);
                    case ".mp3":
                        return PlayMp3(data);
                    default:
                        LastError = "Unsupported audio format: " + ext;
                        Console.WriteLine("[SoundDriver] " + LastError);
                        return false;
                }
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Console.WriteLine("[SoundDriver] PlayFile error: " + e.Message);
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  پخش WAV از byte[] (از دیسک یا Embedded Resource)
        //  از پارسر خود Cosmos (MemoryAudioStream.FromWave) استفاده می‌کند —
        //  فقط PCM آنکد نشده (uncompressed LPCM) پشتیبانی می‌شود که رایج‌ترین
        //  حالت فایل‌های WAV است.
        // ─────────────────────────────────────────────────────────────
        public static bool PlayWav(byte[] wavBytes)
        {
            if (!IsInitialized && !Initialize())
                return false;

            try
            {
                StopInternal();

                var stream = MemoryAudioStream.FromWave(wavBytes);
                _currentStream = stream;
                _seekableStream = stream;
                _mixer.Streams.Add(stream);
                IsPlaying = true;

                // MemoryAudioStream.FromWave فرمت را از هدر WAV پارس می‌کند اما
                // آن را به بیرون بازنمی‌گرداند، بنابراین خودمان هدر WAV را
                // به‌صورت سبک می‌خوانیم تا بتوانیم زمان سپری‌شده/کل را محاسبه کنیم.
                ParseWavHeaderInfo(wavBytes, out int sampleRate, out int channels, out int bytesPerSample);
                _currentSampleRate = sampleRate;
                _currentChannels = channels;
                _currentBytesPerSample = bytesPerSample;

                Console.WriteLine("[SoundDriver] Playing WAV (" + wavBytes.Length + " bytes)");
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Console.WriteLine("[SoundDriver] WAV decode/play error: " + e.Message);
                return false;
            }
        }

        // پارس سبک هدر WAV (chunk "fmt ") فقط برای استخراج sampleRate/channels/bitsPerSample.
        // این پارس مستقل از پارسر داخلی Cosmos است و فقط برای گزارش زمان استفاده می‌شود.
        private static void ParseWavHeaderInfo(byte[] wav, out int sampleRate, out int channels, out int bytesPerSample)
        {
            // مقادیر پیش‌فرض امن در صورت شکست پارس
            sampleRate = 44100;
            channels = 2;
            bytesPerSample = 2;

            try
            {
                int pos = 12; // رد شدن از "RIFF" + size(4) + "WAVE"
                while (pos + 8 <= wav.Length)
                {
                    string chunkId = "" + (char)wav[pos] + (char)wav[pos + 1] + (char)wav[pos + 2] + (char)wav[pos + 3];
                    int chunkSize = BitConverter.ToInt32(wav, pos + 4);
                    int dataStart = pos + 8;

                    if (chunkId == "fmt " && dataStart + 16 <= wav.Length)
                    {
                        short numChannels = BitConverter.ToInt16(wav, dataStart + 2);
                        int sr = BitConverter.ToInt32(wav, dataStart + 4);
                        short bitsPerSample = BitConverter.ToInt16(wav, dataStart + 14);

                        channels = numChannels;
                        sampleRate = sr;
                        bytesPerSample = Math.Max(1, bitsPerSample / 8);
                        return;
                    }

                    pos = dataStart + chunkSize + (chunkSize % 2); // chunk‌ها روی مرز 2 بایتی align می‌شوند
                }
            }
            catch
            {
                // در صورت هر خطایی، مقادیر پیش‌فرض بالا استفاده می‌شوند
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  پخش MP3 از byte[]
        //  ابتدا کل فایل با Mp3Decoder به PCM 16-bit خام تبدیل می‌شود،
        //  سپس مثل یک WAV در حافظه پخش می‌شود.
        // ─────────────────────────────────────────────────────────────
        public static bool PlayMp3(byte[] mp3Bytes)
        {
            if (!IsInitialized && !Initialize())
                return false;

            try
            {
                Console.WriteLine("[SoundDriver] Decoding MP3 (" + mp3Bytes.Length + " bytes)...");

                byte[] pcm = Mp3Decoder.Decode(mp3Bytes, out int sampleRate, out int channels);

                if (pcm == null || pcm.Length == 0)
                {
                    LastError = "MP3 decode produced no audio data (invalid/unsupported file?)";
                    Console.WriteLine("[SoundDriver] " + LastError);
                    return false;
                }

                StopInternal();

                var format = new SampleFormat(AudioBitDepth.Bits16, (byte)channels, true);
                var stream = new MemoryAudioStream(format, (uint)sampleRate, pcm);
                _currentStream = stream;
                _seekableStream = stream;
                _mixer.Streams.Add(stream);
                IsPlaying = true;

                _currentSampleRate = sampleRate;
                _currentChannels = channels;
                _currentBytesPerSample = 2; // Mp3Decoder همیشه PCM 16-bit خروجی می‌دهد

                Console.WriteLine("[SoundDriver] Playing MP3 → " + sampleRate + "Hz, " +
                    channels + "ch, " + pcm.Length + " PCM bytes");
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Console.WriteLine("[SoundDriver] MP3 decode/play error: " + e.Message);
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  کنترل پخش
        // ─────────────────────────────────────────────────────────────
        public static void Stop()
        {
            StopInternal();
        }

        private static void StopInternal()
        {
            try
            {
                if (_currentStream != null && _mixer != null)
                    _mixer.Streams.Remove(_currentStream);
            }
            catch { }
            _currentStream = null;
            _seekableStream = null;
            _currentSampleRate = 0;
            _currentChannels = 0;
            IsPlaying = false;
            IsPaused = false;
            if (_gain != null) _gain.Gain = _currentVolume; // گین را برای پخش بعدی برمی‌گردانیم
        }

        /// <summary>ولوم از 0.0 (بی‌صدا) تا 1.0 (کامل). مقادیر بالاتر ممکن است باعث clipping شوند.</summary>
        public static void SetVolume(float volume)
        {
            if (volume < 0f) volume = 0f;
            if (volume > 1.5f) volume = 1.5f;
            _currentVolume = volume;
            if (_gain != null) _gain.Gain = volume;
        }

        // ─────────────────────────────────────────────────────────────
        //  پاز / ادامه‌ی پخش
        //  توجه: AudioMixer/AudioManager در Cosmos معمولاً یک متد Pause
        //  داخلی ندارند، بنابراین ساده‌ترین و مطمئن‌ترین راه، صفر کردن
        //  گین (بی‌صدا کردن) و متوقف نگه‌داشتن استریم در جای فعلی‌اش است؛
        //  چون MemoryAudioStream خودش Position/Length دارد و تا وقتی از
        //  Mixer خارج نشود، جایش را از دست نمی‌دهد.
        // ─────────────────────────────────────────────────────────────
        public static void Pause()
        {
            if (!IsPlaying || IsPaused) return;
            if (_gain != null) _gain.Gain = 0f;
            IsPaused = true;
        }

        public static void Resume()
        {
            if (!IsPaused) return;
            if (_gain != null) _gain.Gain = _currentVolume;
            IsPaused = false;
        }

        // ─────────────────────────────────────────────────────────────
        //  پیشرفت / زمان پخش
        // ─────────────────────────────────────────────────────────────

        /// <summary>پیشرفت پخش از 0.0 تا 1.0 (بر اساس Position/Length استریم فعلی).</summary>
        public static float GetProgress()
        {
            if (_seekableStream != null && _seekableStream.Length > 0)
                return (float)_seekableStream.Position / _seekableStream.Length;
            return 0f;
        }

        /// <summary>زمان سپری‌شده از آهنگ فعلی، بر حسب ثانیه.</summary>
        public static double GetElapsedSeconds()
        {
            if (_seekableStream != null)
            {
                long bytesPerSecond = (long)_currentSampleRate * _currentChannels * _currentBytesPerSample;
                if (bytesPerSecond > 0)
                    return (double)_seekableStream.Position / bytesPerSecond;
            }
            return 0d;
        }

        /// <summary>مدت زمان کل آهنگ فعلی، بر حسب ثانیه.</summary>
        public static double GetDurationSeconds()
        {
            if (_seekableStream != null)
            {
                long bytesPerSecond = (long)_currentSampleRate * _currentChannels * _currentBytesPerSample;
                if (bytesPerSecond > 0)
                    return (double)_seekableStream.Length / bytesPerSecond;
            }
            return 0d;
        }

        /// <summary>پرش به نقطه‌ای از آهنگ بر اساس کسری 0.0 تا 1.0 از طول کل.</summary>
        public static void SeekToFraction(float fraction)
        {
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;

            if (_seekableStream != null && _seekableStream.Length > 0)
            {
                try
                {
                    _seekableStream.Position = (uint)(_seekableStream.Length * fraction);
                }
                catch (Exception e)
                {
                    LastError = e.Message;
                    Console.WriteLine("[SoundDriver] Seek error: " + e.Message);
                }
            }
        }

        public static bool HasFinished()
        {
            // یک AudioStream فایل که به انتها رسیده دیگر داده تولید نمی‌کند؛
            // MemoryAudioStream مربوط به فایل، وقتی Position == Length برسد تمام شده است.
            if (_seekableStream != null)
                return _seekableStream.Position >= _seekableStream.Length;
            return !IsPlaying;
        }
    }
}
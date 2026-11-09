// Clip recorder for computers, built on FFmpeg (free, open source): F9 starts and stops.
// The game's picture is read back from the graphics card every frame and fed to FFmpeg as raw
// video (H.264, near lossless), the game's sound is taken from the AudioListener, and both are
// joined into one MP4 in Videos/Flappy Crix. F12 saves a screenshot to Pictures/Flappy Crix.
//
// FFmpeg is found next to the game (ffmpeg/ffmpeg.exe), in the game's data folder, or on PATH.
// On Windows the game can download it (BtbN's open-source builds) the first time.
// Phones: use the phone's own screen recorder (it's better there).
// Part of Flappy Crix (Unity) - CRIX STUDIOS. Made with AI (Claude by Anthropic).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace FlappyCrix
{
    public sealed class ClipRecorder : MonoBehaviour
    {
        public const string FfmpegZip = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        public static bool Recording { get; private set; }
        public static float Seconds => Recording ? Time.unscaledTime - startedAt : 0;
        public static bool Downloading { get; private set; }
        public static event Action<string> Notice;

        const int Fps = 60, MaxHeight = 1080;
        static float startedAt;

        Process proc;
        Stream stdin;
        Thread writer;
        readonly Queue<byte[]> frames = new Queue<byte[]>();
        readonly object gate = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        RenderTexture grab, small;
        int w, h, sampleRate, channels;
        double nextFrameAt;
        string videoTmp, audioTmp, finalPath, ffmpeg;
        BinaryWriter wav;
        long wavSamples;
        volatile bool capturingAudio;
        byte[] lastFrame;
        int pending;

        static void Say(string s) { if (Notice != null) Notice(s); }

        public static bool Supported => Settings.IsDesktop && SystemInfo.supportsAsyncGPUReadback;

        void Update()
        {
            if (!Settings.IsDesktop) return;
            if (Input.GetKeyDown(KeyCode.F9)) Toggle();
            if (Input.GetKeyDown(KeyCode.F12)) StartCoroutine(Screenshot());
        }

        public void Toggle()
        {
            if (Recording) Stop();
            else if (!Supported) Say("Clip recording isn't available on this device.");
            else if (Downloading) Say("Still downloading FFmpeg...");
            else
            {
                ffmpeg = FindFfmpeg();
                if (ffmpeg != null) Begin();
                else if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor) StartCoroutine(Download());
                else Say("Clips need FFmpeg: install it (brew install ffmpeg / apt install ffmpeg) and press F9 again.");
            }
        }

        // ------------------------------------------------------------------ ffmpeg

        static string ToolsDir => Path.Combine(Application.persistentDataPath, "ffmpeg");

        static string FindFfmpeg()
        {
            string exe = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor ? "ffmpeg.exe" : "ffmpeg";
            var places = new List<string>
            {
                Path.Combine(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "ffmpeg"), exe),
                Path.Combine(ToolsDir, exe),
                "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/usr/bin/ffmpeg",
            };
            foreach (var p in places) if (File.Exists(p)) return p;
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                try { var p = Path.Combine(dir.Trim(), exe); if (File.Exists(p)) return p; } catch (Exception) { }
            }
            return null;
        }

        IEnumerator Download()
        {
            Downloading = true;
            Say("Getting FFmpeg for clips (about 150 MB, once)...");
            string zip = Path.Combine(Application.temporaryCachePath, "ffmpeg.zip");
            using (var req = UnityWebRequest.Get(FfmpegZip))
            {
                req.downloadHandler = new DownloadHandlerFile(zip);
                var op = req.SendWebRequest();
                float said = 0;
                while (!op.isDone)
                {
                    if (req.downloadProgress - said > 0.25f) { said = req.downloadProgress; Say("FFmpeg " + Mathf.RoundToInt(said * 100) + "%"); }
                    yield return null;
                }
                if (req.result != UnityWebRequest.Result.Success) { Downloading = false; Say("Couldn't download FFmpeg: " + req.error); yield break; }
            }
            string err = null;
            var t = new Thread(() =>
            {
                try
                {
                    Directory.CreateDirectory(ToolsDir);
                    using (var a = new ZipArchive(File.OpenRead(zip), ZipArchiveMode.Read))
                        foreach (var e in a.Entries)
                            if (e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                                using (var src = e.Open())
                                using (var dst = File.Create(Path.Combine(ToolsDir, "ffmpeg.exe")))
                                    src.CopyTo(dst);
                    File.Delete(zip);
                }
                catch (Exception ex) { err = ex.Message; }
            });
            t.Start();
            while (t.IsAlive) yield return null;
            Downloading = false;
            if (err != null || FindFfmpeg() == null) { Say("Couldn't unpack FFmpeg: " + err); yield break; }
            Say("FFmpeg ready. Press F9 to record.");
        }

        // ------------------------------------------------------------------ recording

        void Begin()
        {
            h = Mathf.Min(MaxHeight, Screen.height) & ~1;
            w = Mathf.RoundToInt(Screen.width * (h / (float)Screen.height)) & ~1;
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Flappy Crix");
            if (string.IsNullOrEmpty(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos))) dir = Path.Combine(Application.persistentDataPath, "Clips");
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            finalPath = Path.Combine(dir, "Flappy Crix " + stamp + ".mp4");
            videoTmp = Path.Combine(Application.temporaryCachePath, "clip-video.mp4");
            audioTmp = Path.Combine(Application.temporaryCachePath, "clip-audio.wav");

            // DirectX, Metal and Vulkan read the picture upside down
            string flip = SystemInfo.graphicsUVStartsAtTop ? "-vf vflip " : "";
            string args = "-y -loglevel error -f rawvideo -pix_fmt rgba -s " + w + "x" + h + " -r " + Fps + " -i - " + flip +
                          "-c:v libx264 -preset veryfast -crf 17 -pix_fmt yuv420p -movflags +faststart \"" + videoTmp + "\"";
            try
            {
                proc = Process.Start(new ProcessStartInfo(ffmpeg, args)
                { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true });
                stdin = proc.StandardInput.BaseStream;
                proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning("[ffmpeg] " + e.Data); };
                proc.BeginErrorReadLine();
            }
            catch (Exception e) { Say("Couldn't start FFmpeg: " + e.Message); return; }

            grab = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            sampleRate = AudioSettings.outputSampleRate;
            int bufLen, bufCount;
            AudioSettings.GetDSPBufferSize(out bufLen, out bufCount);
            channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            wav = new BinaryWriter(File.Create(audioTmp));
            WavHeader(wav, sampleRate, channels, 0);
            wavSamples = 0;
            lastFrame = null;
            Recording = true;
            capturingAudio = true;
            startedAt = Time.unscaledTime;
            nextFrameAt = Time.unscaledTimeAsDouble;
            writer = new Thread(WriteLoop) { IsBackground = true, Name = "Clip writer" };
            writer.Start();
            StartCoroutine(CaptureLoop());
            Sfx.Play("select", 0.6f);
            Say("Recording - F9 to stop");
        }

        IEnumerator CaptureLoop()
        {
            var eof = new WaitForEndOfFrame();
            while (Recording)
            {
                yield return eof;
                if (!Recording) break;
                double now = Time.unscaledTimeAsDouble;
                if (now < nextFrameAt) continue;
                // frames the game was too slow to draw are repeated, so the clip keeps real time
                int count = 1 + (int)Math.Floor((now - nextFrameAt) * Fps);
                count = Math.Min(count, 6);
                nextFrameAt += count / (double)Fps;
                if (grab.width != Screen.width || grab.height != Screen.height) { Stop(); Say("Window size changed: clip saved."); yield break; }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(grab);
                Graphics.Blit(grab, small);
                if (pending > 8) { EnqueueRepeat(count); continue; }   // the graphics card is behind: repeat the last frame
                pending++;
                int n = count;
                AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, r =>
                {
                    pending--;
                    if (!Recording || r.hasError) return;
                    var data = r.GetData<byte>().ToArray();
                    lock (gate) { for (int i = 0; i < n; i++) frames.Enqueue(data); lastFrame = data; }
                    wake.Set();
                });
            }
        }

        void EnqueueRepeat(int n)
        {
            lock (gate) { if (lastFrame != null) for (int i = 0; i < n; i++) frames.Enqueue(lastFrame); }
            wake.Set();
        }

        void WriteLoop()
        {
            while (true)
            {
                byte[] f = null;
                lock (gate) { if (frames.Count > 0) f = frames.Dequeue(); }
                if (f == null)
                {
                    if (!Recording && pending <= 0) break;
                    wake.WaitOne(50);
                    continue;
                }
                try { stdin.Write(f, 0, f.Length); }
                catch (Exception) { break; }
            }
        }

        void OnAudioFilterRead(float[] data, int ch)
        {
            var bw = wav;
            if (!capturingAudio || bw == null) return;
            try
            {
                lock (bw)
                {
                    if (!capturingAudio) return;
                    for (int i = 0; i < data.Length; i++)
                    {
                        float s = Mathf.Clamp(data[i], -1f, 1f);
                        bw.Write((short)(s * 32767));
                    }
                    wavSamples += data.Length;
                    if (ch != channels) channels = ch;
                }
            }
            catch (Exception) { }
        }

        public void Stop()
        {
            if (!Recording) return;
            Recording = false;
            capturingAudio = false;
            Sfx.Play("select", 0.6f);
            Say("Saving clip...");
            StartCoroutine(Finish());
        }

        IEnumerator Finish()
        {
            float until = Time.unscaledTime + 2;
            while (pending > 0 && Time.unscaledTime < until) yield return null;
            wake.Set();
            while (writer != null && writer.IsAlive) yield return null;
            try { stdin.Close(); } catch (Exception) { }
            while (proc != null && !proc.HasExited) yield return null;
            lock (wav)
            {
                wav.Seek(0, SeekOrigin.Begin);
                WavHeader(wav, sampleRate, channels, wavSamples);
                wav.Close();
                wav = null;
            }
            if (grab != null) { grab.Release(); Destroy(grab); }
            if (small != null) { small.Release(); Destroy(small); }

            // join picture and sound
            Process mux = null;
            try
            {
                mux = Process.Start(new ProcessStartInfo(ffmpeg,
                    "-y -loglevel error -i \"" + videoTmp + "\" -i \"" + audioTmp + "\" -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart \"" + finalPath + "\"")
                { UseShellExecute = false, CreateNoWindow = true });
            }
            catch (Exception) { }
            while (mux != null && !mux.HasExited) yield return null;
            if (!File.Exists(finalPath) && File.Exists(videoTmp)) File.Copy(videoTmp, finalPath, true);
            try { File.Delete(videoTmp); File.Delete(audioTmp); } catch (Exception) { }
            Say(File.Exists(finalPath) ? "Clip saved: Videos/Flappy Crix" : "The clip couldn't be saved.");
        }

        static void WavHeader(BinaryWriter b, int rate, int ch, long samples)
        {
            long bytes = samples * 2;
            b.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            b.Write((int)(36 + bytes));
            b.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            b.Write(16); b.Write((short)1); b.Write((short)ch); b.Write(rate); b.Write(rate * ch * 2); b.Write((short)(ch * 2)); b.Write((short)16);
            b.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            b.Write((int)bytes);
        }

        IEnumerator Screenshot()
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Flappy Crix");
            if (string.IsNullOrEmpty(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures))) dir = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "Flappy Crix " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Sfx.Play("click");
            Say("Screenshot saved: Pictures/Flappy Crix");
        }

        void OnDestroy() { if (Recording) { Recording = false; try { stdin.Close(); } catch (Exception) { } } }
    }
}

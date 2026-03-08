using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VisionEditCV.Models;

namespace VisionEditCV.Api
{
    public class Sam3Client
    {
        public string BaseUrl { get; set; } =
            "https://8000-dep-01khgcb8hf1kcdc87pbkv4bfz1-d.cloudspaces.litng.ai";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private static readonly HttpClient _healthHttp = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static string EncodeImageToBase64(string imagePath)
        {
            byte[] bytes = File.ReadAllBytes(imagePath);
            return Convert.ToBase64String(bytes);
        }

        private static SegmentationResult ParseResponse(string json)
        {
            var result = new SegmentationResult();
            var root = JObject.Parse(json);

            var masksToken = root["masks"];
            if (masksToken is JArray masksArray)
            {
                foreach (var maskToken in masksArray)
                {
                    if (maskToken is not JArray outerArr) continue;

                    JArray rows;

                    if (outerArr.Count == 1 && outerArr[0] is JArray innerArr
                        && innerArr.Count > 0 && innerArr[0] is JArray)
                    {
                        rows = innerArr;
                    }
                    else if (outerArr.Count > 0 && outerArr[0] is JArray firstRow
                             && firstRow.Count > 0 && firstRow[0] is JArray)
                    {
                        rows = (JArray)outerArr[0];
                    }
                    else
                    {
                        rows = outerArr;
                    }

                    int h = rows.Count;
                    int w = h > 0 && rows[0] is JArray r0 ? r0.Count : 0;
                    var mask = new float[h, w];
                    for (int r = 0; r < h; r++)
                    {
                        var rowArr = (JArray)rows[r];
                        for (int c = 0; c < w; c++)
                            mask[r, c] = rowArr[c].Value<float>();
                    }
                    result.Masks.Add(mask);
                }
            }

            var boxesToken = root["boxes"];
            if (boxesToken is JArray boxesArray)
            {
                foreach (var box in boxesArray)
                {
                    if (box is JArray ba)
                        result.Boxes.Add(ba.Select(v => v.Value<float>()).ToArray());
                }
            }

            var scoresToken = root["scores"];
            if (scoresToken is JArray scoresArray)
            {
                foreach (var s in scoresArray)
                    result.Scores.Add(s.Value<float>());
            }

            return result;
        }

        private string NormalizedUrl()
        {
            string url = BaseUrl.TrimEnd('/');
            return url;
        }

        private static readonly string _debugLogPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "sam3_debug.txt");

        private static void DebugLog(string text)
        {
            try { File.AppendAllText(_debugLogPath, text + "\n"); } catch { }
        }

        private async Task<SegmentationResult?> PostAsync(string endpoint, object payload)
        {
            string url = $"{NormalizedUrl()}{endpoint}";
            string jsonBody = JsonConvert.SerializeObject(payload);

            string logBody = System.Text.RegularExpressions.Regex.Replace(
                jsonBody, @"""image""\s*:\s*""[^""]{50}[^""]*""",
                m => m.Value.Substring(0, m.Value.IndexOf('"', m.Value.IndexOf("image") + 7) + 51) + "...[truncated]\"");
            DebugLog($"\n=== POST {url} ===");
            DebugLog($"REQUEST BODY (image truncated):\n{logBody}");

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.PostAsync(url, content);
            }
            catch (HttpRequestException ex)
            {
                DebugLog($"CONNECTION ERROR: {ex.Message}");
                throw new InvalidOperationException(
                    $"Could not connect to SAM3 server at {NormalizedUrl()}.\n\n{ex.Message}", ex);
            }
            catch (TaskCanceledException)
            {
                DebugLog("TIMEOUT");
                throw new InvalidOperationException(
                    "Request timed out. The server may be starting up (up to 6-7 min). Please try again.");
            }

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                DebugLog($"HTTP ERROR {(int)response.StatusCode}: {body}");
                throw new InvalidOperationException(
                    $"Server returned HTTP {(int)response.StatusCode}: {response.ReasonPhrase}\n{body}");
            }

            string responseJson = await response.Content.ReadAsStringAsync();

            DebugLog($"RESPONSE (first 2000 chars):\n{responseJson.Substring(0, Math.Min(responseJson.Length, 2000))}");

            var result = ParseResponse(responseJson);

            DebugLog($"PARSED: {result.Masks.Count} masks, {result.Boxes.Count} boxes, {result.Scores.Count} scores");
            if (result.Masks.Count > 0)
                DebugLog($"  First mask shape: [{result.Masks[0].GetLength(0)}, {result.Masks[0].GetLength(1)}]");

            return result;
        }

        public async Task<bool> WaitForHealthAsync(
            IProgress<string> onStatus,
            CancellationToken ct)
        {
            string url = $"{NormalizedUrl()}/health";
            int attempt = 0;

            while (!ct.IsCancellationRequested)
            {
                attempt++;
                onStatus.Report($"Connecting… attempt {attempt}");
                try
                {
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attemptCts.CancelAfter(TimeSpan.FromSeconds(15));

                    var response = await _healthHttp.GetAsync(url, attemptCts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        onStatus.Report("Connected");
                        return true;
                    }
                    onStatus.Report(
                        $"Server returned HTTP {(int)response.StatusCode} — retrying…");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    onStatus.Report($"Connecting… attempt {attempt}");
                }

                try { await Task.Delay(5_000, ct); }
                catch (OperationCanceledException) { break; }
            }

            onStatus.Report("Disconnected");
            return false;
        }

        public Task<SegmentationResult?> SegmentWithTextAsync(string imagePath, string prompt)
        {
            string imageB64 = EncodeImageToBase64(imagePath);
            var payload = new { image = imageB64, prompt };
            return PostAsync("/predict-image-text", payload);
        }

        public Task<SegmentationResult?> SegmentWithBBoxAsync(
            string imagePath, float[][] boxes, bool[] labels)
        {
            string imageB64 = EncodeImageToBase64(imagePath);
            var payload = new { image = imageB64, boxes, labels };
            return PostAsync("/predict-bounding-box", payload);
        }
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VisionEditCV.Models;

namespace VisionEditCV.Api
{
    /// <summary>
    /// Async HTTP client for the SAM3 REST API.
    /// Wraps /predict-image-text and /predict-bounding-box endpoints.
    /// </summary>
    public class Sam3Client
    {
        public string BaseUrl { get; set; } =
            "https://8000-dep-01khgcb8hf1kcdc87pbkv4bfz1-d.cloudspaces.litng.ai";

        // 10-minute timeout for prediction calls (model cold-start can take several minutes)
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // Per-attempt timeout for /health polling (15 s per ping, loop retries for up to 10 min)
        private static readonly HttpClient _healthHttp = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string EncodeImageToBase64(string imagePath)
        {
            byte[] bytes = File.ReadAllBytes(imagePath);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Decodes the JSON response from the API into a SegmentationResult.
        /// The API returns:
        ///   { "masks": [ [[float,...], ...], ... ],   // list of 2-D arrays
        ///     "boxes": [ [x,y,w,h], ... ],
        ///     "scores": [ float, ... ] }
        /// </summary>
        private static SegmentationResult ParseResponse(string json)
        {
            var result = new SegmentationResult();
            var root = JObject.Parse(json);

            // ── Masks ────────────────────────────────────────────────────────
            // The API may return masks shaped (H, W) or (1, H, W) per mask entry.
            // Detection: if maskToken[0][0] is a JArray  → shape is (1, H, W), unwrap
            //            if maskToken[0] is a JArray but [0][0] is not → shape is (H, W)
            //            This covers both FastSAM shapes.
            var masksToken = root["masks"];
            if (masksToken is JArray masksArray)
            {
                foreach (var maskToken in masksArray)
                {
                    if (maskToken is not JArray outerArr) continue;

                    JArray rows;
                    // Check if this is (1, H, W): outerArr.Count == 1 and outerArr[0] is an array of arrays
                    if (outerArr.Count == 1 && outerArr[0] is JArray innerArr
                        && innerArr.Count > 0 && innerArr[0] is JArray)
                    {
                        // Shape (1, H, W) — peel off the leading dimension
                        rows = innerArr;
                    }
                    else if (outerArr.Count > 0 && outerArr[0] is JArray firstRow
                             && firstRow.Count > 0 && firstRow[0] is JArray)
                    {
                        // Shape (1, H, W) with count != 1 (shouldn't happen, but guard anyway)
                        rows = (JArray)outerArr[0];
                    }
                    else
                    {
                        // Shape (H, W) — use as-is
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

            // ── Boxes ────────────────────────────────────────────────────────
            var boxesToken = root["boxes"];
            if (boxesToken is JArray boxesArray)
            {
                foreach (var box in boxesArray)
                {
                    if (box is JArray ba)
                        result.Boxes.Add(ba.Select(v => v.Value<float>()).ToArray());
                }
            }

            // ── Scores ───────────────────────────────────────────────────────
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

            // ── DEBUG: log request (truncate base64 image for readability) ────
            string logBody = System.Text.RegularExpressions.Regex.Replace(
                jsonBody, @"""image""\s*:\s*""[^""]{50}[^""]*""",
                m => m.Value.Substring(0, m.Value.IndexOf('"', m.Value.IndexOf("image") + 7) + 51) + "...[truncated]\"");
            DebugLog($"\n=== POST {url} ===");
            DebugLog($"REQUEST BODY (image truncated):\n{logBody}");
            // ─────────────────────────────────────────────────────────────────

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

            // ── DEBUG: log response (first 2000 chars) ────────────────────────
            DebugLog($"RESPONSE (first 2000 chars):\n{responseJson.Substring(0, Math.Min(responseJson.Length, 2000))}");
            // ─────────────────────────────────────────────────────────────────

            var result = ParseResponse(responseJson);

            // ── DEBUG: log parsed result summary ─────────────────────────────
            DebugLog($"PARSED: {result.Masks.Count} masks, {result.Boxes.Count} boxes, {result.Scores.Count} scores");
            if (result.Masks.Count > 0)
                DebugLog($"  First mask shape: [{result.Masks[0].GetLength(0)}, {result.Masks[0].GetLength(1)}]");
            // ─────────────────────────────────────────────────────────────────

            return result;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Polls GET /health until the server responds 200 or the
        /// <paramref name="ct"/> is triggered.
        /// Reports progress via <paramref name="onStatus"/>
        /// (called on a thread-pool thread — marshal to UI if needed).
        /// Returns true when healthy, false if cancelled.
        /// </summary>
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
                    var response = await _healthHttp.GetAsync(url, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        onStatus.Report("Connected");
                        return true;
                    }
                    onStatus.Report(
                        $"Server returned HTTP {(int)response.StatusCode} — retrying…");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    onStatus.Report($"Connecting… attempt {attempt}");
                }

                try { await Task.Delay(10_000, ct); }
                catch (OperationCanceledException) { break; }
            }

            onStatus.Report("Disconnected");
            return false;
        }

        /// <summary>Segment an image using a text prompt.</summary>
        public Task<SegmentationResult?> SegmentWithTextAsync(string imagePath, string prompt)
        {
            string imageB64 = EncodeImageToBase64(imagePath);
            var payload = new { image = imageB64, prompt };
            return PostAsync("/predict-image-text", payload);
        }

        /// <summary>Segment an image using one or more bounding boxes.</summary>
        /// <param name="boxes">Each box is [x, y, w, h] in image-space coordinates.</param>
        /// <param name="labels">True = foreground, False = background, one per box.</param>
        public Task<SegmentationResult?> SegmentWithBBoxAsync(
            string imagePath, float[][] boxes, bool[] labels)
        {
            string imageB64 = EncodeImageToBase64(imagePath);
            var payload = new { image = imageB64, boxes, labels };
            return PostAsync("/predict-bounding-box", payload);
        }
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace SmallBazar.Services.ChatGpt;

/// <summary>Represents a model offered by the authenticated account's public catalog.</summary>
public sealed record ChatGptModel(string Slug, string DisplayName);

/// <summary>Uses only the public Responses API with no automatic inference retries.</summary>
public sealed class ChatGptInference(HttpClient http, ChatGptSession session, Action<string> diagnostic = null)
{
    public async Task<List<ChatGptModel>> ModelsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ChatGptProtocol.Resource + "/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await session.AccessTokenAsync(cancellationToken));
        using var response = await http.SendAsync(request, cancellationToken);
        CheckResponse(response);
        var catalog = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return catalog["models"].AsArray().Where(item => (string)item["visibility"] == "list")
            .Select(item => new ChatGptModel((string)item["slug"], (string)item["display_name"]))
            .Where(item => !string.IsNullOrWhiteSpace(item.Slug)).ToList();
    }

    /// <summary>Runs exactly one synthetic request. Business descriptions and report storage are never accessed.</summary>
    public async Task<string> TestAsync(ChatGptModel model, CancellationToken cancellationToken)
        => await ExecuteAsync(model, null, null, cancellationToken);

    /// <summary>Runs one full classification request using the same validated Responses transport.</summary>
    public Task<string> GenerateAsync(ChatGptModel model, string instructions, string input, CancellationToken cancellationToken)
        => ExecuteAsync(model, instructions, input, cancellationToken);

    private async Task<string> ExecuteAsync(ChatGptModel model, string instructions, string input, CancellationToken cancellationToken)
    {
        var stage = "reading the protected session";
        try { return await TestCoreAsync(model, cancellationToken, value => stage = value, instructions, input); }
        catch (Exception ex) when (ex is not ChatGptException and not OperationCanceledException)
        {
            var exceptionTypes = ex.GetType().Name;
            if (ex.InnerException != null) exceptionTypes += " / " + ex.InnerException.GetType().Name;
            var failure = $"Synthetic test failed while {stage} ({exceptionTypes}). No report data was changed. The test was not retried.";
            diagnostic?.Invoke(failure);
            throw new ChatGptException(failure);
        }
    }

    private async Task<string> TestCoreAsync(ChatGptModel model, CancellationToken cancellationToken, Action<string> stage,
        string instructions, string input)
    {
        if (model == null) throw new ChatGptException("Load models and select one before running the test.");
        var token = await session.AccessTokenAsync(cancellationToken);
        stage("preparing the request");
        var payload = new JsonObject
        {
            ["model"] = model.Slug,
            ["instructions"] = instructions ?? "Classify storage boxes. Return exactly the compact classification requested, without markdown or explanation.",
            ["input"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["content"] = input ?? "Synthetic boxes: 1: hammer. 2: screwdriver. Both belong to Tools. Reply exactly: Tools:1,2"
            }),
            ["store"] = false,
            ["stream"] = true
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, ChatGptProtocol.Resource + "/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        stage("sending the request and waiting for response headers");
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        using var responseLifetime = new ResponseResource(response, diagnostic);
        stage("checking response headers");
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!response.IsSuccessStatusCode || (mediaType != null &&
            !string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase)))
        {
            stage("reading an unexpected response");
            var failure = await DescribeUnexpectedResponseAsync(response, cancellationToken);
            diagnostic?.Invoke(failure);
            throw new ChatGptException(failure);
        }
        if (mediaType == null)
            diagnostic?.Invoke("OpenAI response: HTTP 200 with no Content-Type. Validating the event stream through its terminal event.");
        stage("opening the response stream");
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var streamLifetime = new ResponseResource(stream, diagnostic);
        stage("reading the response events");
        string text;
        try { text = await ReadCompletedTextAsync(stream, cancellationToken); }
        catch (ChatGptException ex)
        {
            diagnostic?.Invoke("Synthetic generation failed: " + ex.Message);
            throw;
        }
        stage("validating the completed classification");
        if (input == null && text.Trim() != "Tools:1,2") throw new ChatGptException("The completed test response did not match the synthetic classification. The test was not retried.");
        diagnostic?.Invoke(input == null ? "Synthetic generation completed. Terminal success and Tools:1,2 classification verified." : "Classification stream completed successfully.");
        return text.Trim();
    }

    /// <summary>Describes an unexpected response without disclosing its body, headers or free-form server messages.</summary>
    public static async Task<string> DescribeUnexpectedResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        var safeMediaType = mediaType switch
        {
            "application/json" or "application/problem+json" or "text/event-stream" or "text/html" or "text/plain" or "application/octet-stream" => mediaType,
            null => "missing content type",
            _ => "other content type"
        };
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var buffer = new char[16385];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(length), cancellationToken);
            if (read == 0) break;
            length += read;
        }
        var kind = "unrecognized response body";
        var code = "no recognized error code";
        if (length > 16384) kind = "response exceeds diagnostic limit";
        else
        {
            var body = new string(buffer, 0, length).TrimStart();
            if (body.StartsWith("data:", StringComparison.Ordinal) || body.StartsWith("event:", StringComparison.Ordinal))
                kind = "event framing with unexpected content type";
            else if (body.Length == 0) kind = "empty response";
            else
            {
                try
                {
                    var json = JsonNode.Parse(body);
                    kind = "JSON response";
                    if (json is JsonObject root)
                    {
                        var error = root["error"] as JsonObject;
                        var candidate = error?["code"] as JsonValue ?? root["code"] as JsonValue ?? root["error"] as JsonValue;
                        if (candidate != null && candidate.TryGetValue<string>(out var returnedCode)) code = SafeErrorCode(returnedCode);
                        if (root["error"] != null) kind = "JSON error";
                        else if ((string)root["object"] == "response" && (string)root["status"] == "completed") kind = "completed JSON response instead of event stream";
                    }
                }
                catch (System.Text.Json.JsonException) { }
                catch (InvalidOperationException) { }
            }
        }
        var recovery = code switch
        {
            "invalid_api_key" or "invalid_token" => "Continue with ChatGPT again; do not enter an API key.",
            "insufficient_quota" or "subscription_sharing_usage_limit_exceeded" => "Check the ChatGPT plan usage limit.",
            "subscription_sharing_usage_unavailable" or "access_denied" => "ChatGPT plan usage is unavailable or not authorized for this request.",
            "model_not_found" => "Reload the available models and select a model offered by this account.",
            _ => "The generation was not confirmed."
        };
        return $"OpenAI response: HTTP {(int)response.StatusCode}, {safeMediaType}, {kind}, {code}. {recovery} The test was not retried.";
    }

    private static string SafeErrorCode(string code) => code switch
    {
        "invalid_api_key" or "invalid_token" or "insufficient_quota" or "model_not_found" or
        "unsupported_parameter" or "invalid_request_error" or "invalid_request" or "access_denied" or
        "subscription_sharing_usage_limit_exceeded" or "subscription_sharing_usage_unavailable" or
        "rate_limit_exceeded" or "server_error" or "permission_denied" or "unsupported_value" or
        "invalid_value" or "context_length_exceeded" or "insufficient_permissions" => code,
        _ => "unrecognized error code"
    };

    /// <summary>Closes transport resources without masking a terminal result or the original protocol failure.</summary>
    private sealed class ResponseResource(IDisposable resource, Action<string> diagnostic) : IDisposable
    {
        public void Dispose()
        {
            try { resource.Dispose(); }
            catch (Exception ex) { diagnostic?.Invoke($"Response cleanup failed ({ex.GetType().Name}); the original generation result was preserved."); }
        }
    }

    private static ChatGptException StreamFailure(JsonNode item)
    {
        var error = item["response"]?["error"] as JsonObject ?? item["error"] as JsonObject;
        var codeValue = error?["code"] as JsonValue ?? item["code"] as JsonValue;
        var code = codeValue != null && codeValue.TryGetValue<string>(out var value)
            ? SafeErrorCode(value) : "no recognized error code";
        var recovery = code switch
        {
            "subscription_sharing_usage_limit_exceeded" or "rate_limit_exceeded" or "insufficient_quota" => "Check ChatGPT Settings → Usage and try again later.",
            "subscription_sharing_usage_unavailable" or "permission_denied" or "insufficient_permissions" => "ChatGPT plan usage is unavailable or not authorized for this request.",
            "model_not_found" => "Reload the account's models and select an available model.",
            _ => "The generation did not complete."
        };
        return new ChatGptException($"OpenAI stream failure ({code}). {recovery} No report data was changed. The test was not retried.");
    }

    /// <summary>Accepts text only from a completed response, rejecting refusals, failures and truncated streams.</summary>
    public static async Task<string> ReadCompletedTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var data = new StringBuilder();
        var streamedText = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length > 1024 * 1024 || data.Length > 2 * 1024 * 1024)
                throw new ChatGptException("The response exceeded the test size limit.");
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line[5..].TrimStart(' '));
            }
            if (line.Length != 0 || data.Length == 0) continue;
            if (data.ToString() == "[DONE]") break;
            var item = JsonNode.Parse(data.ToString());
            data.Clear();
            var type = (string)item["type"];
            if (type == "response.output_text.delta")
            {
                streamedText.Append((string)item["delta"]);
                if (streamedText.Length > 2 * 1024 * 1024) throw new ChatGptException("The classification exceeded the size limit.");
            }
            if (type is "response.refusal.delta" or "response.refusal.done")
                throw new ChatGptException("The model declined the request.");
            if (type is "response.failed" or "response.incomplete" or "error")
                throw StreamFailure(item);
            if (type == "response.completed")
            {
                var response = item["response"];
                if ((string)response["status"] != "completed" || response["error"] != null)
                    throw new ChatGptException("The generation did not complete successfully.");
                var text = new StringBuilder();
                foreach (var output in response["output"]?.AsArray() ?? new JsonArray())
                {
                    if ((string)output["type"] != "message") continue;
                    if ((string)output["status"] != "completed") throw new ChatGptException("The response contains an incomplete message.");
                    foreach (var content in output["content"].AsArray())
                    {
                        if ((string)content["type"] == "refusal") throw new ChatGptException("The model declined the request.");
                        if ((string)content["type"] == "output_text") text.Append((string)content["text"]);
                    }
                }
                if (text.Length > 0 && streamedText.Length > 0 && text.ToString() != streamedText.ToString())
                    throw new ChatGptException("The completed response does not match its streamed classification. The test was not retried.");
                if (text.Length == 0) text.Append(streamedText);
                if (text.Length == 0) throw new ChatGptException("The generation completed without classification text.");
                return text.ToString();
            }
        }
        throw new ChatGptException("The stream ended before successful completion. The request was not retried.");
    }

    private static void CheckResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new ChatGptException((int)response.StatusCode switch
        {
            401 => "The ChatGPT session is no longer valid. Continue with ChatGPT again.",
            403 => "This account or app is not authorized to use the ChatGPT plan. Check plan eligibility and app permission.",
            429 => "The ChatGPT plan limit was reached. Check usage in ChatGPT settings and try again later.",
            _ => "OpenAI could not complete the request. Try again explicitly later. No generation was retried."
        });
    }
}

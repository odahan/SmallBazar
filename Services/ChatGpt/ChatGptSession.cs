using System.Net;
using System.Text.Json.Nodes;

namespace SmallBazar.Services.ChatGpt;

/// <summary>Stores OAuth credentials in platform-protected, non-transferable local storage.</summary>
public interface IChatGptCredentialStore
{
    Task<string> ReadAsync();
    Task WriteAsync(string value);
}

/// <summary>Opens the authorization URL in the user's system browser.</summary>
public interface IChatGptBrowser
{
    Task OpenAsync(string url);
}

public sealed record ChatGptAccount(string ClientId, string Label);

/// <summary>Manages one prototype registration, independently of business data and report caches.</summary>
public sealed class ChatGptSession(HttpClient http, IChatGptCredentialStore store, IChatGptBrowser browser,
    Action<string> diagnostic = null)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public string LastSignInStatus { get; private set; } = "No sign-in attempt in this session.";
    public event Action<string> SignInProgress;

    /// <summary>Publishes only fixed messages and sanitized protocol errors, never credentials or callback URLs.</summary>
    private void ReportSignIn(string message)
    {
        LastSignInStatus = message;
        diagnostic?.Invoke(message);
        SignInProgress?.Invoke(message);
    }

    public async Task<string> AccountAsync()
    {
        var saved = await ReadAsync();
        if (saved["access_token"] == null) return "Not connected";
        return (string)saved["email"] + (ChatGptProtocol.HasPlanScope((string)saved["scope"])
            ? " — ChatGPT plan authorized" : " — ChatGPT plan permission missing");
    }

    public async Task SignInAsync(CancellationToken cancellationToken, CancellationToken userCancellationToken = default, bool addAccount = false)
    {
        await gate.WaitAsync(cancellationToken);
        var stage = "reading secure storage";
        try
        {
            var saved = await ReadAsync();
            if (addAccount)
            {
                var accounts = SavedAccounts(saved);
                saved = new JsonObject { ["host_id"] = (string)saved["host_id"], ["accounts"] = accounts };
            }
            var hostId = (string)saved["host_id"];
            if (hostId == null)
            {
                saved["host_id"] = hostId = "urn:uuid:" + Guid.NewGuid().ToString();
                await store.WriteAsync(saved.ToJsonString());
            }
            var state = ChatGptProtocol.RandomValue();
            var nonce = ChatGptProtocol.RandomValue();
            var verifier = ChatGptProtocol.RandomValue();
            using var callback = new LoopbackCallback();
            var pending = callback.ReceiveAsync(cancellationToken);
            try
            {
                stage = "waiting for the browser callback";
                ReportSignIn("Waiting for sign-in in the system browser. Return to this same SmallBazar window.");
                var authorizationUrl = ChatGptProtocol.AuthorizationUrl(callback.RedirectUri, hostId, state,
                    nonce, verifier, (string)saved["client_id"], (string)saved["email"]);
                if (saved["client_id"] != null && (bool?)saved["plan_permission_declined"] == true) authorizationUrl += "&prompt=consent";
                await browser.OpenAsync(authorizationUrl);
                var returned = await pending;
                stage = "validating the browser callback";
                ReportSignIn("Browser callback received. Validating the authorization result…");
                var result = ChatGptProtocol.ValidateCallback(returned, callback.RedirectUri, state, (string)saved["client_id"]);
                stage = "exchanging the authorization code";
                ReportSignIn("Browser authorization validated. Exchanging the authorization code with OpenAI…");
                var tokens = await ExchangeAsync(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code", ["client_id"] = result.ClientId,
                    ["code"] = result.Code, ["code_verifier"] = verifier,
                    ["redirect_uri"] = callback.RedirectUri, ["resource"] = ChatGptProtocol.Resource
                }, cancellationToken);
                stage = "loading OpenAI signing keys";
                ReportSignIn("Authorization code exchanged. Loading OpenAI signing keys…");
                var keys = await ReadJsonAsync(ChatGptProtocol.Issuer + "/.well-known/jwks.json", cancellationToken);
                stage = "validating OpenAI identity";
                ReportSignIn("Verifying the signed OpenAI identity…");
                var identity = ChatGptProtocol.ValidateIdToken((string)tokens["id_token"], keys, result.ClientId, nonce, DateTimeOffset.UtcNow);
                if (saved["subject"] != null && (string)saved["subject"] != identity.Subject)
                    throw new ChatGptException("This registration belongs to a different ChatGPT account. The existing session was preserved.");
                foreach (var name in new[] { "access_token", "refresh_token", "id_token", "scope", "expires_at" }) saved.Remove(name);
                ApplyTokens(saved, tokens);
                saved["plan_permission_declined"] = !ChatGptProtocol.HasPlanScope((string)saved["scope"]);
                saved["client_id"] = result.ClientId;
                saved["subject"] = identity.Subject;
                saved["email"] = identity.Email;
                stage = "saving the protected session";
                await store.WriteAsync(saved.ToJsonString());
                stage = "verifying protected storage";
                var persisted = await ReadAsync();
                if ((string)persisted["access_token"] != (string)saved["access_token"] ||
                    (string)persisted["client_id"] != result.ClientId)
                    throw new ChatGptException("The protected session could not be read back after saving.");
                ReportSignIn(ChatGptProtocol.HasPlanScope((string)saved["scope"])
                    ? "Connected. Identity verified, ChatGPT plan permission granted, and protected session saved."
                    : "Identity verified and saved, but ChatGPT plan permission was not granted. Inference is disabled.");
            }
            finally
            {
                callback.Dispose();
                try { await pending; }
                catch (Exception) { }
            }
        }
        catch (Exception ex)
        {
            var reason = ex switch
            {
                ChatGptException safe => safe.Message,
                OperationCanceledException when userCancellationToken.IsCancellationRequested =>
                    "Sign-in was cancelled in SmallBazar. Start a new sign-in instead of reloading the old browser callback.",
                OperationCanceledException when userCancellationToken.CanBeCanceled =>
                    "Sign-in timed out. Return to SmallBazar and choose Continue with ChatGPT to start a new attempt.",
                OperationCanceledException => "The operation was cancelled or timed out.",
                HttpRequestException => "OpenAI could not be reached. Check the network.",
                System.Text.Json.JsonException => "OpenAI returned an unexpected response format.",
                _ => "The operation could not be completed on this device."
            };
            ReportSignIn($"Sign-in failed while {stage}. {reason}");
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task<bool> HasSessionAsync()
    {
        var saved = await ReadAsync();
        return saved["access_token"] != null && ChatGptProtocol.HasPlanScope((string)saved["scope"]);
    }

    public async Task<List<ChatGptAccount>> AccountsAsync()
    {
        var saved = await ReadAsync();
        return SavedAccounts(saved).Select(item => new ChatGptAccount((string)item["client_id"],
            ((string)item["email"] ?? "ChatGPT account") + " · " + ((string)item["client_id"])[^Math.Min(8, ((string)item["client_id"]).Length)..])).ToList();
    }

    public async Task SelectAccountAsync(string clientId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var saved = await ReadAsync();
            var accounts = SavedAccounts(saved);
            var selected = accounts.FirstOrDefault(item => (string)item["client_id"] == clientId);
            if (selected == null) throw new ChatGptException("This saved ChatGPT account is no longer available.");
            var next = selected.DeepClone().AsObject();
            next["host_id"] = (string)saved["host_id"];
            next["accounts"] = new JsonArray(accounts.Where(item => (string)item["client_id"] != clientId).Select(item => item.DeepClone()).ToArray());
            await store.WriteAsync(next.ToJsonString());
        }
        finally { gate.Release(); }
    }

    public async Task<string> SelectedModelAsync() => (string)(await ReadAsync())["model_slug"];

    /// <summary>Restores the saved report model locally, including registrations created before names were stored.</summary>
    public async Task<ChatGptModel> SelectedModelDetailsAsync()
    {
        var saved = await ReadAsync();
        var slug = (string)saved["model_slug"];
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var displayName = (string)saved["model_display_name"];
        return new ChatGptModel(slug, string.IsNullOrWhiteSpace(displayName) ? slug : displayName);
    }
    public async Task<string> ActiveClientIdAsync() => (string)(await ReadAsync())["client_id"];

    public async Task SelectModelAsync(ChatGptModel model, CancellationToken cancellationToken)
    {
        if (model == null) throw new ChatGptException("Select an available model first.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            var saved = await ReadAsync();
            saved["model_slug"] = model.Slug;
            saved["model_display_name"] = model.DisplayName;
            await store.WriteAsync(saved.ToJsonString());
        }
        finally { gate.Release(); }
    }

    private static JsonArray SavedAccounts(JsonObject saved)
    {
        var accounts = saved["accounts"]?.DeepClone().AsArray() ?? new JsonArray();
        var clientId = (string)saved["client_id"];
        if (clientId != null)
        {
            var current = saved.DeepClone().AsObject();
            current.Remove("accounts");
            current.Remove("host_id");
            for (var i = accounts.Count - 1; i >= 0; i--)
                if ((string)accounts[i]["client_id"] == clientId) accounts.RemoveAt(i);
            accounts.Add(current);
        }
        return accounts;
    }

    /// <summary>Renews only the selected session, serializing rotating refresh tokens.</summary>
    public async Task<string> AccessTokenAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var saved = await ReadAsync();
            if (saved["access_token"] == null)
                throw new ChatGptException("Continue with ChatGPT before running the synthetic test.");
            if (!ChatGptProtocol.HasPlanScope((string)saved["scope"]))
                throw new ChatGptException("ChatGPT plan permission is missing. Continue with ChatGPT and authorize plan usage. An eligible plan is required.");
            if ((long?)saved["expires_at"] <= DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds())
            {
                if (saved["refresh_token"] == null) throw new ChatGptException("The session has expired. Continue with ChatGPT again.");
                JsonObject tokens;
                try
                {
                    tokens = await ExchangeAsync(new Dictionary<string, string>
                    {
                        ["grant_type"] = "refresh_token", ["client_id"] = (string)saved["client_id"],
                        ["refresh_token"] = (string)saved["refresh_token"], ["resource"] = ChatGptProtocol.Resource
                    }, cancellationToken);
                }
                catch (ChatGptException ex) when (ex.Code is "invalid_grant" or "invalid_refresh_token" or "token_expired" or "refresh_token_expired" or "refresh_token_invalidated" or "refresh_token_reused")
                {
                    foreach (var name in new[] { "access_token", "refresh_token", "id_token", "scope", "expires_at" }) saved.Remove(name);
                    await store.WriteAsync(saved.ToJsonString());
                    throw new ChatGptException("The renewable session has expired or was revoked. Continue with ChatGPT again. Your saved report was preserved.");
                }
                ApplyTokens(saved, tokens);
                await store.WriteAsync(saved.ToJsonString());
                if (!ChatGptProtocol.HasPlanScope((string)saved["scope"]))
                    throw new ChatGptException("ChatGPT plan usage is no longer authorized. Continue with ChatGPT again.");
            }
            return (string)saved["access_token"];
        }
        finally { gate.Release(); }
    }

    /// <summary>Attempts remote revocation and always clears local tokens while retaining registration and host identity.</summary>
    public async Task<bool> SignOutAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var saved = await ReadAsync();
            var confirmed = saved["refresh_token"] == null;
            try
            {
                if (!confirmed)
                {
                    var metadata = await ReadJsonAsync(ChatGptProtocol.Issuer + "/.well-known/openid-configuration", cancellationToken);
                    var endpoint = new Uri((string)metadata["revocation_endpoint"]);
                    if (endpoint.Scheme != "https" || endpoint.Host != "auth.openai.com" || !endpoint.IsDefaultPort)
                        throw new ChatGptException("The revocation endpoint is invalid.");
                    for (var attempt = 0; attempt < 2; attempt++)
                    {
                        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["token"] = (string)saved["refresh_token"], ["token_type_hint"] = "refresh_token",
                            ["client_id"] = (string)saved["client_id"]
                        });
                        using var response = await http.PostAsync(endpoint, body, cancellationToken);
                        confirmed = response.StatusCode == HttpStatusCode.OK;
                        if (confirmed || (int)response.StatusCode < 500) break;
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (Exception) { confirmed = false; }
            foreach (var name in new[] { "access_token", "refresh_token", "id_token", "scope", "expires_at" }) saved.Remove(name);
            await store.WriteAsync(saved.ToJsonString());
            return confirmed;
        }
        finally { gate.Release(); }
    }

    private async Task<JsonObject> ReadAsync()
    {
        var value = await store.ReadAsync();
        return value == null ? new JsonObject() : JsonNode.Parse(value).AsObject();
    }

    private async Task<JsonObject> ReadJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new ChatGptException("OpenAI account information is unavailable. Try again later.");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).AsObject();
    }

    private async Task<JsonObject> ExchangeAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var body = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync(ChatGptProtocol.Issuer + "/api/accounts/oauth/token", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var reason = "authorization_failed";
            try
            {
                var error = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var code = error?["error"] is JsonObject details ? (string)details["code"] : (string)error?["error"];
                if (code is "invalid_grant" or "invalid_client" or "invalid_request" or "invalid_scope" or "access_denied" or "unauthorized_client" or
                    "invalid_refresh_token" or "token_expired" or "refresh_token_expired" or "refresh_token_invalidated" or "refresh_token_reused")
                    reason = code;
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
            throw new ChatGptException($"OpenAI token exchange failed (HTTP {(int)response.StatusCode}, {reason}). Continue with ChatGPT again.", reason);
        }
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).AsObject();
    }

    private static void ApplyTokens(JsonObject saved, JsonObject tokens)
    {
        if (string.IsNullOrWhiteSpace((string)tokens["access_token"]) ||
            !string.Equals((string)tokens["token_type"], "Bearer", StringComparison.OrdinalIgnoreCase) ||
            (long?)tokens["expires_in"] is not > 0)
            throw new ChatGptException("OpenAI returned an incomplete session. Continue with ChatGPT again.");
        foreach (var name in new[] { "access_token", "refresh_token", "id_token", "scope" })
            if (tokens[name] != null) saved[name] = tokens[name].DeepClone();
        saved["expires_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)tokens["expires_in"];
    }
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SmallBazar.Services.ChatGpt;
using Xunit;

namespace SmallBazar.ChatGpt.Tests;

public sealed class ChatGptTests
{
    [Fact]
    public void PkceMatchesRfc7636Vector()
    {
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            ChatGptProtocol.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void AuthorizationRequestsPlanPermissionWithoutClientSecret()
    {
        var url = ChatGptProtocol.AuthorizationUrl("http://127.0.0.1:1234/auth/callback", "urn:uuid:host", "state", "nonce", "verifier");
        Assert.Contains("client_id=dynamic_agent_client", url);
        Assert.Contains("agent_name_hint=SmallBazar", url);
        Assert.Contains("chatgpt.tokens.use.direct", url);
        Assert.DoesNotContain("client_secret", url);
        Assert.DoesNotContain("agent_name_hint", ChatGptProtocol.AuthorizationUrl("http://127.0.0.1:1234/auth/callback", "urn:uuid:host", "state", "nonce", "verifier", "issued"));
    }

    [Theory]
    [InlineData("state=wrong&code=code&client_id=issued")]
    [InlineData("state=expected&state=expected&code=code&client_id=issued")]
    [InlineData("state=expected&error=access_denied&code=code&client_id=issued")]
    [InlineData("state=expected&code=code")]
    [InlineData("state=expected&code=code&client_id=dynamic_agent_client")]
    public void InvalidCallbacksAreRejected(string query)
    {
        Assert.Throws<ChatGptException>(() => ChatGptProtocol.ValidateCallback(new Uri("http://127.0.0.1:1234/auth/callback?" + query),
            "http://127.0.0.1:1234/auth/callback", "expected", null));
    }

    [Fact]
    public void ReturningCallbackCannotSwitchRegistration()
    {
        Assert.Throws<ChatGptException>(() => ChatGptProtocol.ValidateCallback(new Uri("http://127.0.0.1:1234/auth/callback?state=expected&code=code&client_id=other"),
            "http://127.0.0.1:1234/auth/callback", "expected", "selected"));
        var result = ChatGptProtocol.ValidateCallback(new Uri("http://127.0.0.1:1234/auth/callback?state=expected&code=code"),
            "http://127.0.0.1:1234/auth/callback", "expected", "selected");
        Assert.Equal("selected", result.ClientId);
    }

    [Fact]
    public async Task LoopbackUsesEphemeralIpv4PortAndAcceptsBrowserCallback()
    {
        using var receiver = new LoopbackCallback();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var receive = receiver.ReceiveAsync(timeout.Token);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false });
        var address = receiver.RedirectUri + "?state=test&code=synthetic&client_id=test";
        var response = await client.GetAsync(address, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(address, (await receive).AbsoluteUri);
        Assert.Equal("127.0.0.1", new Uri(receiver.RedirectUri).Host);
        Assert.True(new Uri(receiver.RedirectUri).Port > 0);
    }

    [Fact]
    public async Task LoopbackCancellationStopsWaiting()
    {
        using var receiver = new LoopbackCallback();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => receiver.ReceiveAsync(timeout.Token));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("single-audience-array")]
    [InlineData("multiple-audiences-with-azp")]
    [InlineData("multiple-audiences-without-azp")]
    [InlineData("wrong-azp")]
    [InlineData("wrong-signature")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-nonce")]
    [InlineData("expired")]
    public void IdTokenRequiresSignatureAndOidcClaims(string scenario)
    {
        using var rsa = RSA.Create(2048);
        var key = rsa.ExportParameters(false);
        var jwks = new JsonObject { ["keys"] = new JsonArray(new JsonObject
        {
            ["kid"] = "test", ["kty"] = "RSA", ["alg"] = "RS256",
            ["n"] = ChatGptProtocol.Encode(key.Modulus), ["e"] = ChatGptProtocol.Encode(key.Exponent)
        }) };
        var header = new JsonObject { ["alg"] = "RS256", ["kid"] = "test" };
        var payload = new JsonObject
        {
            ["iss"] = scenario == "wrong-issuer" ? "https://attacker.invalid" : ChatGptProtocol.Issuer,
            ["aud"] = scenario == "wrong-audience" ? "other" : "client",
            ["nonce"] = scenario == "wrong-nonce" ? "other" : "nonce",
            ["exp"] = DateTimeOffset.UtcNow.AddMinutes(scenario == "expired" ? -1 : 5).ToUnixTimeSeconds(),
            ["sub"] = "subject", ["email"] = "synthetic@example.invalid"
        };
        if (scenario == "single-audience-array") payload["aud"] = new JsonArray("client");
        if (scenario.StartsWith("multiple-audiences")) payload["aud"] = new JsonArray("client", "other");
        if (scenario == "multiple-audiences-with-azp") payload["azp"] = "client";
        if (scenario == "wrong-azp") payload["azp"] = "other";
        var unsigned = ChatGptProtocol.Encode(Encoding.UTF8.GetBytes(header.ToJsonString())) + "." +
            ChatGptProtocol.Encode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (scenario == "wrong-signature") signature[0] ^= 1;
        var token = unsigned + "." + ChatGptProtocol.Encode(signature);
        if (scenario is "valid" or "single-audience-array" or "multiple-audiences-with-azp") Assert.Equal("subject", ChatGptProtocol.ValidateIdToken(token, jwks, "client", "nonce", DateTimeOffset.UtcNow).Subject);
        else Assert.Throws<ChatGptException>(() => ChatGptProtocol.ValidateIdToken(token, jwks, "client", "nonce", DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("response.failed")]
    [InlineData("response.incomplete")]
    [InlineData("error")]
    [InlineData("response.output_text.delta")]
    public async Task PartialOrFailedStreamsNeverProduceSuccess(string type)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data: {\"type\":\"" + type + "\",\"delta\":\"partial\"}\n\n"));
        await Assert.ThrowsAsync<ChatGptException>(() => ChatGptInference.ReadCompletedTextAsync(stream, default));
    }

    [Fact]
    public async Task SuccessfulGenerationUsesOneResponsesRequestAndNoPing()
    {
        var handler = new FakeHttpHandler();
        using var http = new HttpClient(handler);
        var store = new FakeStore();
        var session = new ChatGptSession(http, store, new FakeBrowser());
        var inference = new ChatGptInference(http, session);
        Assert.Equal("Tools:1,2", await inference.TestAsync(new ChatGptModel("account-model", "Account model"), default));
        Assert.Equal(1, handler.Calls);
        Assert.Equal("https://api.openai.com/v1/responses", handler.LastUri);
        var body = JsonNode.Parse(handler.Body);
        Assert.False((bool)body["store"]);
        Assert.True((bool)body["stream"]);
        Assert.IsType<JsonArray>(body["input"]);
        Assert.Equal("account-model", (string)body["model"]);
        Assert.DoesNotContain("temperature", handler.Body);
        Assert.DoesNotContain("max_output_tokens", handler.Body);
        Assert.Equal("synthetic-token", handler.Bearer);
    }

    [Fact]
    public async Task MissingPermissionNeverSendsInference()
    {
        var handler = new FakeHttpHandler();
        using var http = new HttpClient(handler);
        var store = new FakeStore { Value = "{\"access_token\":\"test\",\"scope\":\"openid\"}" };
        var inference = new ChatGptInference(http, new ChatGptSession(http, store, new FakeBrowser()));
        await Assert.ThrowsAsync<ChatGptException>(() => inference.TestAsync(new ChatGptModel("test", "Test"), default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task FailedRevocationClearsTokensButPreservesHostAndRegistration()
    {
        var handler = new FakeHttpHandler { Status = HttpStatusCode.ServiceUnavailable };
        using var http = new HttpClient(handler);
        var store = new FakeStore { Value = "{\"host_id\":\"host\",\"client_id\":\"issued\",\"access_token\":\"test\",\"refresh_token\":\"refresh\"}" };
        var session = new ChatGptSession(http, store, new FakeBrowser());
        Assert.False(await session.SignOutAsync(default));
        Assert.Equal("Not connected", await session.AccountAsync());
        Assert.DoesNotContain("token", store.Value);
        Assert.Equal("host", (string)JsonNode.Parse(store.Value)["host_id"]);
        Assert.Equal("issued", (string)JsonNode.Parse(store.Value)["client_id"]);
    }

    [Fact]
    public async Task ConcurrentTokenRequestsRotateRefreshTokenOnlyOnce()
    {
        var calls = 0;
        using var http = new HttpClient(new DelegateHandler(async (request, token) =>
        {
            Interlocked.Increment(ref calls);
            Assert.EndsWith("/api/accounts/oauth/token", request.RequestUri.AbsoluteUri);
            var form = await request.Content.ReadAsStringAsync(token);
            Assert.Contains("refresh_token=old-refresh", form);
            Assert.Contains("client_id=issued", form);
            Assert.DoesNotContain("scope=", form);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"token_type\":\"Bearer\",\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600}")
            };
        }));
        var store = new FakeStore { Value = new JsonObject
        {
            ["client_id"] = "issued", ["access_token"] = "old-access", ["refresh_token"] = "old-refresh",
            ["scope"] = ChatGptProtocol.Scopes, ["expires_at"] = 1
        }.ToJsonString() };
        var session = new ChatGptSession(http, store, new FakeBrowser());
        var tokens = await Task.WhenAll(session.AccessTokenAsync(default), session.AccessTokenAsync(default));
        Assert.All(tokens, token => Assert.Equal("new-access", token));
        Assert.Equal(1, calls);
        Assert.Equal("new-refresh", (string)JsonNode.Parse(store.Value)["refresh_token"]);
    }

    [Fact]
    public async Task BrowserFailurePreservesPreviousSessionAndDoesNotExchangeCode()
    {
        var handler = new FakeHttpHandler();
        using var http = new HttpClient(handler);
        var store = new FakeStore { Value = new JsonObject
        {
            ["host_id"] = "existing-host", ["client_id"] = "issued", ["subject"] = "existing-subject",
            ["access_token"] = "old-access", ["refresh_token"] = "old-refresh", ["scope"] = ChatGptProtocol.Scopes
        }.ToJsonString() };
        var before = store.Value;
        var session = new ChatGptSession(http, store, new FakeBrowser());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SignInAsync(timeout.Token));
        Assert.Equal(before, store.Value);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task UsageLimitDoesNotRetryGeneration()
    {
        var handler = new FakeHttpHandler { Status = HttpStatusCode.TooManyRequests };
        using var http = new HttpClient(handler);
        var store = new FakeStore();
        var before = store.Value;
        var inference = new ChatGptInference(http, new ChatGptSession(http, store, new FakeBrowser()));
        await Assert.ThrowsAsync<ChatGptException>(() => inference.TestAsync(new ChatGptModel("test", "Test"), default));
        Assert.Equal(1, handler.Calls);
        Assert.Equal(before, store.Value);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("missing-client")]
    [InlineData("declined")]
    [InlineData("invalid-grant")]
    [InlineData("wrong-nonce")]
    [InlineData("storage-failure")]
    public async Task FullSignInReportsActualResultWithoutDisclosingCredentials(string scenario)
    {
        using var signingKey = RSA.Create(2048);
        var publicKey = signingKey.ExportParameters(false);
        var jwks = new JsonObject { ["keys"] = new JsonArray(new JsonObject
        {
            ["kid"] = "test", ["kty"] = "RSA", ["alg"] = "RS256",
            ["n"] = ChatGptProtocol.Encode(publicKey.Modulus), ["e"] = ChatGptProtocol.Encode(publicKey.Exponent)
        }) };
        string nonce = null;
        string state = null;
        var tokenCalls = 0;
        var diagnostics = new List<string>();
        using var http = new HttpClient(new DelegateHandler((request, cancellation) =>
        {
            if (request.RequestUri.AbsolutePath.EndsWith("jwks.json"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jwks.ToJsonString()) });
            tokenCalls++;
            if (scenario == "invalid-grant")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"secret-server-detail\"}")
                });
            var header = ChatGptProtocol.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"kid\":\"test\"}"));
            var payload = new JsonObject
            {
                ["iss"] = ChatGptProtocol.Issuer, ["aud"] = new JsonArray("issued"), ["sub"] = "subject",
                ["nonce"] = scenario == "wrong-nonce" ? "wrong" : nonce,
                ["exp"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(), ["email"] = "private@example.invalid"
            };
            var unsigned = header + "." + ChatGptProtocol.Encode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
            var jwt = unsigned + "." + ChatGptProtocol.Encode(signingKey.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            var tokens = new JsonObject
            {
                ["id_token"] = jwt, ["access_token"] = "secret-access", ["refresh_token"] = "secret-refresh",
                ["token_type"] = "Bearer", ["scope"] = ChatGptProtocol.Scopes, ["expires_in"] = 3600
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tokens.ToJsonString()) });
        }));
        var browser = new CallbackBrowser(async url =>
        {
            var parameters = new Uri(url).Query.TrimStart('?').Split('&').Select(item => item.Split('=', 2))
                .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]));
            nonce = parameters["nonce"];
            state = parameters["state"];
            var callback = parameters["redirect_uri"] + "?state=" + Uri.EscapeDataString(state) +
                (scenario == "declined" ? "&error=access_denied" : "&code=secret-code") +
                (scenario == "missing-client" ? "" : "&client_id=issued");
            using var localHttp = new HttpClient(new HttpClientHandler { UseProxy = false });
            using var response = await localHttp.GetAsync(callback);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });
        var store = new FakeStore { Value = "{\"host_id\":\"existing-host\"}", FailWrites = scenario == "storage-failure" };
        var session = new ChatGptSession(http, store, browser, diagnostics.Add);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        if (scenario == "success")
        {
            await session.SignInAsync(timeout.Token);
            Assert.StartsWith("Connected.", session.LastSignInStatus);
            Assert.Contains("ChatGPT plan authorized", await session.AccountAsync());
        }
        else
        {
            await Assert.ThrowsAnyAsync<Exception>(() => session.SignInAsync(timeout.Token));
            var result = session.LastSignInStatus;
            Assert.StartsWith("Sign-in failed while ", result);
            Assert.Equal("Not connected", await session.AccountAsync());
            Assert.Equal(result, session.LastSignInStatus);
            if (scenario == "invalid-grant") Assert.Contains("HTTP 400, invalid_grant", result);
            if (scenario == "storage-failure") Assert.Contains("saving the protected session", result);
        }
        Assert.Equal(scenario is "declined" or "missing-client" ? 0 : 1, tokenCalls);
        foreach (var message in diagnostics)
        {
            Assert.DoesNotContain("secret", message);
            Assert.DoesNotContain("private@example.invalid", message);
            Assert.DoesNotContain(state, message);
            Assert.DoesNotContain(nonce, message);
        }
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":\"subscription_sharing_usage_unavailable\",\"message\":\"secret-server-detail\"}}", "JSON error", "subscription_sharing_usage_unavailable")]
    [InlineData("{\"error\":{\"code\":\"invalid_api_key\",\"message\":\"secret-token\"}}", "JSON error", "invalid_api_key")]
    [InlineData("{\"error\":{\"code\":\"secret-arbitrary-code\"}}", "JSON error", "unrecognized error code")]
    [InlineData("{\"object\":\"response\",\"status\":\"completed\"}", "completed JSON response instead of event stream", "no recognized error code")]
    [InlineData("<html>secret-proxy-detail</html>", "unrecognized response body", "no recognized error code")]
    [InlineData("", "empty response", "no recognized error code")]
    public async Task UnexpectedResponsePreservesSafeProtocolDetailsWithoutRetry(string body, string kind, string code)
    {
        var handler = new FakeHttpHandler { ResponseBody = body, ContentType = "application/json" };
        using var http = new HttpClient(handler);
        var diagnostic = new List<string>();
        var inference = new ChatGptInference(http, new ChatGptSession(http, new FakeStore(), new FakeBrowser()), diagnostic.Add);
        var error = await Assert.ThrowsAsync<ChatGptException>(() => inference.TestAsync(new ChatGptModel("test", "Test"), default));
        Assert.Contains("HTTP 200, application/json", error.Message);
        Assert.Contains(kind, error.Message);
        Assert.Contains(code, error.Message);
        Assert.DoesNotContain("secret", error.Message);
        Assert.Single(diagnostic);
        Assert.Equal(error.Message, diagnostic[0]);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task UnexpectedResponseDiagnosticHasBoundedBodyRead()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', 20000), Encoding.UTF8, "text/plain")
        };
        var result = await ChatGptInference.DescribeUnexpectedResponseAsync(response, default);
        Assert.Contains("response exceeds diagnostic limit", result);
        Assert.DoesNotContain(new string('x', 50), result);
    }

    [Fact]
    public async Task MissingContentTypeAcceptsOnlyCompletedValidatedEventStream()
    {
        var handler = new FakeHttpHandler { OmitContentType = true };
        using var http = new HttpClient(handler);
        var diagnostics = new List<string>();
        var inference = new ChatGptInference(http, new ChatGptSession(http, new FakeStore(), new FakeBrowser()), diagnostics.Add);
        Assert.Equal("Tools:1,2", await inference.TestAsync(new ChatGptModel("test", "Test"), default));
        Assert.Equal(1, handler.Calls);
        Assert.Contains(diagnostics, message => message.Contains("no Content-Type"));
        Assert.Contains(diagnostics, message => message.Contains("Terminal success"));
    }

    [Theory]
    [InlineData("data: {\"type\":\"response.output_text.delta\",\"delta\":\"Tools:1,2\"}\n\n")]
    [InlineData("data: {\"type\":\"response.failed\"}\n\n")]
    [InlineData("{\"object\":\"response\",\"status\":\"completed\"}")]
    [InlineData("")]
    public async Task MissingContentTypeStillRejectsIncompleteAndNonEventResponses(string body)
    {
        var handler = new FakeHttpHandler { OmitContentType = true, ResponseBody = body };
        using var http = new HttpClient(handler);
        var diagnostics = new List<string>();
        var inference = new ChatGptInference(http, new ChatGptSession(http, new FakeStore(), new FakeBrowser()), diagnostics.Add);
        await Assert.ThrowsAsync<ChatGptException>(() => inference.TestAsync(new ChatGptModel("test", "Test"), default));
        Assert.Equal(1, handler.Calls);
        Assert.DoesNotContain(diagnostics, message => message.Contains("Terminal success"));
    }

    [Fact]
    public async Task TextDeltasBecomeAvailableOnlyAfterSuccessfulTerminalEvent()
    {
        var body = "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Tools:\"}\n\n" +
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"1,2\"}\n\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[]}}\n\n";
        var handler = new FakeHttpHandler { OmitContentType = true, ResponseBody = body };
        using var http = new HttpClient(handler);
        var inference = new ChatGptInference(http, new ChatGptSession(http, new FakeStore(), new FakeBrowser()));
        Assert.Equal("Tools:1,2", await inference.TestAsync(new ChatGptModel("test", "Test"), default));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("data: {\"type\":\"response.failed\"}\n\n")]
    [InlineData("data: {\"type\":\"response.incomplete\"}\n\n")]
    [InlineData("data: {\"type\":\"response.refusal.delta\",\"delta\":\"refused\"}\n\n")]
    [InlineData("")]
    public async Task StreamedClassificationIsRejectedWithoutTerminalSuccess(string terminal)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Tools:1,2\"}\n\n" + terminal));
        await Assert.ThrowsAsync<ChatGptException>(() => ChatGptInference.ReadCompletedTextAsync(stream, default));
    }

    [Fact]
    public async Task TerminalClassificationCannotDisagreeWithDeltas()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Tools:1,2\"}\n\n" +
            "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":\"Other:3\"}]}]}}\n\n"));
        await Assert.ThrowsAsync<ChatGptException>(() => ChatGptInference.ReadCompletedTextAsync(stream, default));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SignInDistinguishesExplicitCancellationFromDeadline(bool cancelledByUser)
    {
        using var deadline = new CancellationTokenSource();
        using var userCancellation = new CancellationTokenSource();
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, userCancellation.Token);
        var browser = new CallbackBrowser(url =>
        {
            if (cancelledByUser) userCancellation.Cancel();
            else deadline.Cancel();
            return Task.CompletedTask;
        });
        using var http = new HttpClient(new FakeHttpHandler());
        var store = new FakeStore { Value = "{\"host_id\":\"existing-host\"}" };
        var session = new ChatGptSession(http, store, browser);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SignInAsync(combined.Token, userCancellation.Token));
        Assert.Contains(cancelledByUser ? "Sign-in was cancelled in SmallBazar" : "Sign-in timed out", session.LastSignInStatus);
        Assert.DoesNotContain("cancelled or timed out", session.LastSignInStatus);
        Assert.Equal("Not connected", await session.AccountAsync());
    }

    [Fact]
    public async Task RuntimeTransportFailureReportsStageAndTypeWithoutExceptionMessage()
    {
        using var http = new HttpClient(new DelegateHandler((request, cancellation) =>
            throw new IOException("secret-runtime-detail", new InvalidOperationException("secret-inner-detail"))));
        var diagnostics = new List<string>();
        var inference = new ChatGptInference(http, new ChatGptSession(http, new FakeStore(), new FakeBrowser()), diagnostics.Add);
        var failure = await Assert.ThrowsAsync<ChatGptException>(() => inference.TestAsync(new ChatGptModel("test", "Test"), default));
        Assert.Contains("sending the request and waiting for response headers", failure.Message);
        Assert.Contains("IOException / InvalidOperationException", failure.Message);
        Assert.DoesNotContain("secret", failure.Message);
        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task MalformedEventReportsParsingStageWithoutPayload()
    {
        var handler = new FakeHttpHandler { ResponseBody = "data: secret-invalid-json\n\n" };
        using var http = new HttpClient(handler);
        var inference = new ChatGptInference(http, new ChatGptSession(http, new FakeStore(), new FakeBrowser()));
        var failure = await Assert.ThrowsAsync<ChatGptException>(() => inference.TestAsync(new ChatGptModel("test", "Test"), default));
        Assert.Contains("reading the response events", failure.Message);
        Assert.Contains("JsonReaderException", failure.Message);
        Assert.DoesNotContain("secret", failure.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("subscription_sharing_usage_limit_exceeded")]
    [InlineData("subscription_sharing_usage_unavailable")]
    [InlineData("model_not_found")]
    public async Task StreamFailureRetainsSafeErrorCodeWithoutServerMessage(string code)
    {
        var body = "data: {\"type\":\"response.failed\",\"response\":{\"error\":{\"code\":\"" + code + "\",\"message\":\"secret-server-message\"}}}\n\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        var error = await Assert.ThrowsAsync<ChatGptException>(() => ChatGptInference.ReadCompletedTextAsync(stream, default));
        Assert.Contains(code, error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StreamCleanupCannotMaskSuccessfulOrFailedGeneration(bool completed)
    {
        var body = completed
            ? "data: {\"type\":\"response.output_text.delta\",\"delta\":\"Tools:1,2\"}\n\ndata: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[]}}\n\n"
            : "data: {\"type\":\"error\",\"code\":\"subscription_sharing_usage_unavailable\",\"message\":\"secret-server-message\"}\n\n";
        using var http = new HttpClient(new DelegateHandler((request, cancellation) =>
        {
            var content = new StreamContent(new FailingDisposeStream(body));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }));
        var diagnostics = new List<string>();
        var inference = new ChatGptInference(http, new ChatGptSession(http, new FakeStore(), new FakeBrowser()), diagnostics.Add);
        if (completed) Assert.Equal("Tools:1,2", await inference.TestAsync(new ChatGptModel("test", "Test"), default));
        else
        {
            var error = await Assert.ThrowsAsync<ChatGptException>(() => inference.TestAsync(new ChatGptModel("test", "Test"), default));
            Assert.Contains("subscription_sharing_usage_unavailable", error.Message);
        }
        Assert.Contains(diagnostics, value => value.Contains("Response cleanup failed"));
        Assert.All(diagnostics, value => Assert.DoesNotContain("secret", value));
    }

    private sealed class FailingDisposeStream(string body) : MemoryStream(Encoding.UTF8.GetBytes(body))
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) throw new IOException("secret-cleanup-detail");
        }
    }

    [Fact]
    public async Task SavedModelNameAndSlugSurviveANewSessionWithoutNetworkAccess()
    {
        var store = new FakeStore();
        using var http = new HttpClient(new DelegateHandler((request, cancellation) =>
            throw new InvalidOperationException("No network request is expected while saving or restoring a model.")));
        var model = new ChatGptModel("gpt-6-luna", "GPT-6 Luna");
        await new ChatGptSession(http, store, new FakeBrowser()).SelectModelAsync(model, default);

        var reopened = new ChatGptSession(http, store, new FakeBrowser());

        Assert.Equal(model.Slug, await reopened.SelectedModelAsync());
        Assert.Equal(model, await reopened.SelectedModelDetailsAsync());
    }

    [Fact]
    public async Task LegacyModelSlugCanBeDisplayedWithoutChangingProtectedStorage()
    {
        var store = new FakeStore { Value = new JsonObject { ["model_slug"] = "legacy-model" }.ToJsonString() };
        var original = store.Value;
        using var http = new HttpClient(new FakeHttpHandler());
        var session = new ChatGptSession(http, store, new FakeBrowser());

        Assert.Equal(new ChatGptModel("legacy-model", "legacy-model"), await session.SelectedModelDetailsAsync());
        Assert.Equal(original, store.Value);
    }

    [Fact]
    public async Task MissingModelDoesNotInventADefaultSelection()
    {
        using var http = new HttpClient(new FakeHttpHandler());
        var session = new ChatGptSession(http, new FakeStore(), new FakeBrowser());

        Assert.Null(await session.SelectedModelDetailsAsync());
    }

    [Fact]
    public async Task SwitchingAccountsKeepsEachCredentialAndModelSetSeparate()
    {
        var store = new FakeStore { Value = new JsonObject
        {
            ["host_id"] = "host", ["client_id"] = "first", ["email"] = "same@example.invalid", ["access_token"] = "first-token", ["model_slug"] = "first-model",
            ["accounts"] = new JsonArray(new JsonObject { ["client_id"] = "second", ["email"] = "same@example.invalid", ["access_token"] = "second-token", ["model_slug"] = "second-model" })
        }.ToJsonString() };
        using var http = new HttpClient(new FakeHttpHandler());
        var session = new ChatGptSession(http, store, new FakeBrowser());
        var accounts = await session.AccountsAsync();
        Assert.Equal(2, accounts.Count);
        Assert.NotEqual(accounts[0].Label, accounts[1].Label);
        await session.SelectAccountAsync("second", default);
        Assert.Equal("second-model", await session.SelectedModelAsync());
        await session.SelectModelAsync(new ChatGptModel("second-model", "Second account model"), default);
        Assert.Equal("Second account model", (await session.SelectedModelDetailsAsync()).DisplayName);
        Assert.Equal("second-token", (string)JsonNode.Parse(store.Value)["access_token"]);
        await session.SelectAccountAsync("first", default);
        Assert.Equal("first-token", (string)JsonNode.Parse(store.Value)["access_token"]);
        Assert.Equal("first-model", await session.SelectedModelAsync());
        Assert.Equal("first-model", (await session.SelectedModelDetailsAsync()).DisplayName);
        await session.SelectAccountAsync("second", default);
        Assert.Equal("Second account model", (await session.SelectedModelDetailsAsync()).DisplayName);
        Assert.Equal("host", (string)JsonNode.Parse(store.Value)["host_id"]);
    }

    [Fact]
    public async Task TerminalRefreshErrorClearsOnlyUnusableTokensAndRetainsRegistration()
    {
        var store = new FakeStore { Value = new JsonObject
        {
            ["host_id"] = "host", ["client_id"] = "issued", ["access_token"] = "old-token", ["refresh_token"] = "revoked-token",
            ["scope"] = ChatGptProtocol.Scopes, ["expires_at"] = 1
        }.ToJsonString() };
        using var http = new HttpClient(new DelegateHandler((request, cancellation) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":\"invalid_grant\"}") })));
        var session = new ChatGptSession(http, store, new FakeBrowser());
        await Assert.ThrowsAsync<ChatGptException>(() => session.AccessTokenAsync(default));
        Assert.False(await session.HasSessionAsync());
        Assert.Equal("issued", await session.ActiveClientIdAsync());
        Assert.Equal("host", (string)JsonNode.Parse(store.Value)["host_id"]);
        Assert.DoesNotContain("revoked-token", store.Value);
    }

    private sealed class CallbackBrowser(Func<string, Task> open) : IChatGptBrowser
    {
        public Task OpenAsync(string url) => open(url);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class FakeStore : IChatGptCredentialStore
    {
        public bool FailWrites;
        public string Value = new JsonObject
        {
            ["access_token"] = "synthetic-token", ["scope"] = ChatGptProtocol.Scopes,
            ["expires_at"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        }.ToJsonString();
        public Task<string> ReadAsync() => Task.FromResult(Value);
        public Task WriteAsync(string value)
        {
            if (FailWrites) throw new IOException("Synthetic secure storage failure.");
            Value = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBrowser : IChatGptBrowser
    {
        public Task OpenAsync(string url) => throw new InvalidOperationException("Tests must not open the browser.");
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        public int Calls;
        public string LastUri;
        public string Body;
        public string Bearer;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public string ResponseBody;
        public string ContentType = "text/event-stream";
        public bool OmitContentType;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri.AbsoluteUri;
            Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Bearer = request.Headers.Authorization?.Parameter;
            var content = "data: {\"type\":\"response.completed\",\"response\":{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":\"Tools:1,2\"}]}]}}\n\n";
            var response = new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody ?? content, Encoding.UTF8, ContentType) };
            if (OmitContentType) response.Content.Headers.ContentType = null;
            return response;
        }
    }
}

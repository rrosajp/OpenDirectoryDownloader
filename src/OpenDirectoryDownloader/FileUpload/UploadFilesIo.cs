using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenDirectoryDownloader.Models;
using System.Text.RegularExpressions;

namespace OpenDirectoryDownloader.FileUpload;

public class UploadFilesIo : IFileUploadSite
{
	public string Name => "UFile.io";

	public async Task<IFileUploadSiteFile> UploadFile(HttpClient httpClient, string path)
	{
		int retries = 0;
		int maxRetries = 5;

		while (retries < maxRetries)
		{
			try
			{
				string html = await httpClient.GetStringAsync("https://ufile.io");
				Match regexMatchCSRFToken = new Regex("name=\"csrf_test_name\" value=\"(?<CSRFToken>.*)\"").Match(html);
				Match regexMatchSessionId = new Regex("<input.*?id=\"session_id\".*?value=\"(?<SessionId>.*)\"").Match(html);

				if (regexMatchCSRFToken.Success && regexMatchSessionId.Success)
				{
					string csrfToken = regexMatchCSRFToken.Groups["CSRFToken"].Value;
					string sessionId = regexMatchSessionId.Groups["SessionId"].Value;

					// Create session
					Dictionary<string, string> postValuesCreateSession = new()
					{
						{ "csrf_test_name", csrfToken },
						{ "file_size", new FileInfo(path).Length.ToString() }
					};

					HttpRequestMessage httpRequestMessageCreateSession = new(HttpMethod.Post, "https://up.ufile.io/v1/upload/create_session") { Content = new FormUrlEncodedContent(postValuesCreateSession) };
					HttpResponseMessage httpResponseMessageCreateSession = await httpClient.SendAsync(httpRequestMessageCreateSession);

					JObject resultCreateSession = JObject.Parse(await httpResponseMessageCreateSession.Content.ReadAsStringAsync());

					string fileId = resultCreateSession["fuid"].Value<string>();

					if (string.IsNullOrEmpty(fileId))
					{
						throw new Exception("Ufile.io error, error creating session");
					}

					// Post file
					using FileStream fileStream = new(path, FileMode.Open);
					using StreamContent streamContent = new(fileStream);

					using MultipartFormDataContent multipartFormDataContent = new($"Upload----{Guid.NewGuid()}")
					{
						{ new StringContent("1"), "chunk_index" },
						{ new StringContent(fileId), "fuid" },
						{ streamContent, "file", Path.GetFileName(path) }
					};

					using HttpResponseMessage httpResponseMessage = await httpClient.PostAsync("https://up.ufile.io/v1/upload/chunk", multipartFormDataContent);

					httpResponseMessage.EnsureSuccessStatusCode();

					// Finalise
					Dictionary<string, string> postValuesFinalise = new()
					{
						{ "csrf_test_name", csrfToken },
						{ "fuid", fileId },
						{ "file_name", Path.GetFileName(path) },
						{ "file_type", Path.GetExtension(path) },
						{ "total_chunks", "1" },
						{ "session_id", sessionId },
					};

					HttpRequestMessage httpRequestMessageFinalise = new(HttpMethod.Post, "https://up.ufile.io/v1/upload/finalise") { Content = new FormUrlEncodedContent(postValuesFinalise) };
					HttpResponseMessage httpResponseMessageFinalise = await httpClient.SendAsync(httpRequestMessageFinalise);

					string response = await httpResponseMessageFinalise.Content.ReadAsStringAsync();
					OpenDirectoryIndexer.Session.UploadedUrlsResponse = response;

					return JsonConvert.DeserializeObject<UploadFilesIoFile>(response);
				}

				retries++;
			}
			catch (Exception ex)
			{
				retries++;
				Program.Logger.Error(ex, "Error uploading file, retry in 5 seconds..");
				await Task.Delay(TimeSpan.FromSeconds(5));
			}
		}

		throw new FriendlyException("Error uploading URLs");
	}
}

public class UploadFilesIoFile : IFileUploadSiteFile
{
	[JsonProperty("url")]
	public string Url { get; set; }

	[JsonProperty("id")]
	public long Id { get; set; }

	[JsonProperty("destination")]
	public Uri Destination { get; set; }

	[JsonProperty("name")]
	public string Name { get; set; }

	[JsonProperty("filename")]
	public string Filename { get; set; }

	[JsonProperty("slug")]
	public string Slug { get; set; }

	[JsonProperty("size")]
	public string Size { get; set; }

	[JsonProperty("type")]
	public string Type { get; set; }

	[JsonProperty("expiry")]
	public string Expiry { get; set; }

	[JsonProperty("location")]
	public string Location { get; set; }
}

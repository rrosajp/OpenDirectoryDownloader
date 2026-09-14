using System.Text;
using Google.Apis.Auth.OAuth2;

namespace OpenDirectoryDownloader.GoogleDrive;

/// <summary>
/// A <see cref="LocalServerCodeReceiver"/> which does not crash the OAuth flow when no browser can be
/// launched (e.g. on a headless server). It still starts the local loopback listener and waits for the
/// OAuth redirect as usual, but instead of throwing when launching a browser process fails, it prints the
/// authorization URL (and how to reach it from a headless server) so the flow can be completed manually.
/// </summary>
public class HeadlessLocalServerCodeReceiver : LocalServerCodeReceiver
{
	protected override bool OpenBrowser(string url)
	{
		try
		{
			if (base.OpenBrowser(url))
			{
				return true;
			}
		}
		catch (Exception)
		{
			// No browser to launch (e.g. headless server). Fall through to printing instructions below,
			// and report success so the caller doesn't turn this into a fatal NotSupportedException.
		}

		Console.WriteLine(BuildManualInstructions(url));

		return true;
	}

	private static string BuildManualInstructions(string url)
	{
		StringBuilder stringBuilder = new();

		stringBuilder.AppendLine();
		stringBuilder.AppendLine("Could not open a browser automatically (this looks like a headless environment).");
		stringBuilder.AppendLine("Open this URL manually in any browser to authorize Google Drive access:");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine(url);
		stringBuilder.AppendLine();

		string port = GetRedirectPort(url);

		if (!string.IsNullOrWhiteSpace(port))
		{
			stringBuilder.AppendLine("If this is a remote/headless server, forward the port first, e.g.:");
			stringBuilder.AppendLine($"    ssh -L {port}:localhost:{port} <user>@<server>");
			stringBuilder.AppendLine("then open the URL above on your local machine.");
			stringBuilder.AppendLine();
		}

		stringBuilder.AppendLine("Waiting for authorization...");

		return stringBuilder.ToString();
	}

	private static string GetRedirectPort(string url)
	{
		try
		{
			Uri uri = new(url);
			System.Collections.Specialized.NameValueCollection queryParameters = System.Web.HttpUtility.ParseQueryString(uri.Query);
			string redirectUri = queryParameters["redirect_uri"];

			return !string.IsNullOrWhiteSpace(redirectUri) ? new Uri(redirectUri).Port.ToString() : null;
		}
		catch (Exception)
		{
			return null;
		}
	}
}

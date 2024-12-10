// frmMain.AuthAndDownload.cs
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using System.Net;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // HttpClient with CookieContainer to manage sessions
        private static readonly HttpClientHandler handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true
        };

        private static readonly HttpClient client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://gladiuscommunity.com/")
        };

        // Nested Authentication Form
        private class AuthForm : Form
        {
            private frmMain mainForm;
            private WebView2 webViewAuth;

            public AuthForm(frmMain mainForm)
            {
                this.mainForm = mainForm;
                this.Text = "Authenticate with Discord";
                this.Width = 1200;
                this.Height = 700;

                webViewAuth = new WebView2
                {
                    Dock = DockStyle.Fill
                };
                webViewAuth.NavigationStarting += WebViewAuth_NavigationStarting;
                webViewAuth.CoreWebView2InitializationCompleted += WebViewAuth_CoreWebView2InitializationCompleted;
                webViewAuth.NavigationCompleted += WebViewAuth_NavigationCompleted;

                this.Controls.Add(webViewAuth);

                this.Load += AuthForm_Load;
            }

            private async void AuthForm_Load(object sender, EventArgs e)
            {
                mainForm.AppendLog($"Starting Authentication", InfoColor, mainForm.rtbPatchingApplicationOutput);
                await webViewAuth.EnsureCoreWebView2Async();
                webViewAuth.Source = new Uri("https://gladiuscommunity.com/auth/discord");
            }

            // Logging navigation events
            private void WebViewAuth_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
            {
                // mainForm.AppendLog($"Starting Authentication", InfoColor, mainForm.rtbPatchingApplicationOutput);
            }

            private void WebViewAuth_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
            {
                if (e.IsSuccess)
                {
                    // mainForm.AppendLog("WebView2 initialized successfully.", SuccessColor, mainForm.rtbPatchingApplicationOutput);
                }
                else
                {
                    // mainForm.AppendLog($"WebView2 initialization failed: {e.InitializationException.Message}", ErrorColor, mainForm.rtbPatchingApplicationOutput);
                }
            }

            private async void WebViewAuth_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                // mainForm.AppendLog($"Navigation completed: {webViewAuth.Source}", SuccessColor, mainForm.rtbPatchingApplicationOutput);

                if (webViewAuth.Source.AbsoluteUri.StartsWith("https://gladiuscommunity.com/auth/discord/callback"))
                {
                    // mainForm.AppendLog("Authentication callback detected. Awaiting final redirect...", InfoColor, mainForm.rtbPatchingApplicationOutput);
                    // Do not attempt to extract cookies here; wait for the final redirect
                }
                else if (webViewAuth.Source.AbsoluteUri == "https://gladiuscommunity.com/")
                {
                   //  mainForm.AppendLog("Redirected to base URL after authentication. Verifying session...", InfoColor, mainForm.rtbPatchingApplicationOutput);

                    // Extract cookies from WebView2 and sync with HttpClient
                    var cookies = await webViewAuth.CoreWebView2.CookieManager.GetCookiesAsync("https://gladiuscommunity.com/");
                    foreach (var cookie in cookies)
                    {
                        // mainForm.AppendLog($"WebView2 Cookie: {cookie.Name} = {cookie.Value}", InfoColor, mainForm.rtbPatchingApplicationOutput);

                        // Ensure domain and path are correctly set
                        string domain = string.IsNullOrEmpty(cookie.Domain) ? "gladiuscommunity.com" : cookie.Domain.TrimStart('.');
                        string path = string.IsNullOrEmpty(cookie.Path) ? "/" : cookie.Path;

                        handler.CookieContainer.Add(
                            new Uri("https://gladiuscommunity.com/"),
                            new Cookie(cookie.Name, cookie.Value, path, domain)
                        );

                        // mainForm.AppendLog($"Synchronizing cookie: {cookie.Name} = {cookie.Value}", InfoColor, mainForm.rtbPatchingApplicationOutput);
                    }

                    // Check if the session cookie exists
                    Uri uri = new Uri("https://gladiuscommunity.com/");
                    CookieCollection httpClientCookies = handler.CookieContainer.GetCookies(uri);
                    foreach (Cookie c in httpClientCookies)
                    {
                        // mainForm.AppendLog($"HttpClient Cookie: {c.Name} = {c.Value}", InfoColor, mainForm.rtbPatchingApplicationOutput);
                    }

                    if (httpClientCookies["connect.sid"] != null)
                    {
                        mainForm.AppendLog("Authentication successful. Closing AuthForm.", SuccessColor, mainForm.rtbPatchingApplicationOutput);

                        // Set DialogResult to OK to indicate success
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        mainForm.AppendLog("Authentication failed. Session cookie not found. Make sure you are an approved modder!", ErrorColor, mainForm.rtbPatchingApplicationOutput);
                        MessageBox.Show("Authentication failed. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // 2. Loading File List
        private async Task LoadFileListAsync()
        {
            try
            {
                AppendLog("Attempting to load file list...", InfoColor, rtbPatchingApplicationOutput);

                HttpResponseMessage response = await client.GetAsync("xdelta/list");

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    AppendLog("Successfully received file list JSON.", SuccessColor, rtbPatchingApplicationOutput);

                    List<XDeltaFile> files = JsonConvert.DeserializeObject<List<XDeltaFile>>(json);

                    if (files == null || !files.Any())
                    {
                        AppendLog("No files found in the list.", InfoColor, rtbPatchingApplicationOutput);
                        return;
                    }

                    // Group by mod and sort by uploadedDate descending
                    var groupedFiles = files
                        .GroupBy(f => f.mod)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            ModName = g.Key,
                            Files = g.OrderByDescending(f => f.uploadedDate).ToList()
                        });

                    tvwxdeltaFiles.Nodes.Clear();

                    foreach (var group in groupedFiles)
                    {
                        TreeNode modNode = new TreeNode(group.ModName);
                        AppendLog($"Adding mod node: {group.ModName}", InfoColor, rtbPatchingApplicationOutput);

                        foreach (var file in group.Files)
                        {
                            TreeNode fileNode = new TreeNode(file.fileName)
                            {
                                Tag = file
                            };
                            modNode.Nodes.Add(fileNode);
                        }

                        tvwxdeltaFiles.Nodes.Add(modNode);
                    }

                    AppendLog("TreeView populated.", SuccessColor, rtbPatchingApplicationOutput);
                }
                else
                {
                    string errorMsg = await response.Content.ReadAsStringAsync();
                    AppendLog($"Failed to load file list. Status Code: {response.StatusCode}, Message: {errorMsg}", ErrorColor, rtbPatchingApplicationOutput);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Exception in LoadFileListAsync: {ex.Message}", ErrorColor, rtbPatchingApplicationOutput);
            }
        }

        // 3. Uploading Files
        private async void btnPatchingUpload_Click(object sender, EventArgs e)
        {
            await UploadFileAsync();
        }

        private async Task UploadFileAsync()
        {
            Uri uri = new Uri("https://gladiuscommunity.com/");
            CookieCollection cookies = handler.CookieContainer.GetCookies(uri);
            bool isAuthenticated = cookies["connect.sid"] != null;

            // Log all HttpClient cookies (optional)
            foreach (Cookie c in handler.CookieContainer.GetCookies(uri))
            {
                // AppendLog($"HttpClient Cookie: {c.Name} = {c.Value}", InfoColor, rtbPatchingApplicationOutput);
            }

            if (!isAuthenticated)
            {
                AppendLog("Authentication required to upload files.", ErrorColor, rtbPatchingApplicationOutput);
                using (AuthForm authForm = new AuthForm(this))
                {
                    if (authForm.ShowDialog() != DialogResult.OK)
                    {
                        AppendLog("Authentication failed or canceled. Cannot proceed with upload.", ErrorColor, rtbPatchingApplicationOutput);
                        MessageBox.Show("Authentication is required to upload files.", "Authentication Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                // Re-check authentication after AuthForm
                cookies = handler.CookieContainer.GetCookies(uri);
                isAuthenticated = cookies["connect.sid"] != null;

                if (!isAuthenticated)
                {
                    AppendLog("Post-authentication check failed. Session cookie not found.", ErrorColor, rtbPatchingApplicationOutput);
                    MessageBox.Show("Authentication failed. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            using (OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "xDelta Files (*.xdelta)|*.xdelta",
                Title = "Select an xDelta File to Upload"
            })
            {
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    string fileName = Path.GetFileName(filePath);

                    try
                    {
                        using (var multipartContent = new MultipartFormDataContent())
                        {
                            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                            var fileContent = new ByteArrayContent(fileBytes);
                            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                            multipartContent.Add(fileContent, "file", fileName);

                            HttpResponseMessage response = await client.PostAsync("xdelta/upload", multipartContent);
                            string responseString = await response.Content.ReadAsStringAsync();

                            if (response.IsSuccessStatusCode)
                            {
                                AppendLog($"File '{fileName}' uploaded successfully.", SuccessColor, rtbPatchingApplicationOutput);
                                MessageBox.Show("File uploaded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                await LoadFileListAsync();
                            }
                            else if (response.StatusCode == HttpStatusCode.Unauthorized)
                            {
                                // Handle session expiration
                                AppendLog("Session expired during upload. Re-authenticating...", ErrorColor, rtbPatchingApplicationOutput);
                                MessageBox.Show("Session expired. Please authenticate again.", "Session Expired", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                using (AuthForm authForm = new AuthForm(this))
                                {
                                    if (authForm.ShowDialog() == DialogResult.OK)
                                    {
                                        AppendLog("Re-authentication successful. Retrying upload.", InfoColor, rtbPatchingApplicationOutput);
                                        await UploadFileAsync(); // Retry upload
                                    }
                                    else
                                    {
                                        AppendLog("Re-authentication canceled. Upload aborted.", ErrorColor, rtbPatchingApplicationOutput);
                                        MessageBox.Show("Re-authentication canceled. Upload aborted.", "Upload Aborted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    }
                                }
                            }
                            else if (response.StatusCode == HttpStatusCode.Forbidden)
                            {
                                // Handle lack of permissions
                                AppendLog("Upload failed: User does not have permission to upload files.", ErrorColor, rtbPatchingApplicationOutput);
                                MessageBox.Show("You are not an approved modder and cannot upload files. Please reach out on Discord to get this resolved.", "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                            {
                                // Handle other errors
                                AppendLog($"Upload failed: {responseString}", ErrorColor, rtbPatchingApplicationOutput);
                                MessageBox.Show($"Upload failed: {responseString}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Error uploading file: {ex.Message}", ErrorColor, rtbPatchingApplicationOutput);
                        MessageBox.Show($"Error uploading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // 4. Downloading Files
        private async void tvwxdeltaFiles_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Level == 1) // File node
            {
                XDeltaFile selectedFile = e.Node.Tag as XDeltaFile;
                if (selectedFile != null)
                {
                    var confirmResult = MessageBox.Show($"Do you want to download '{selectedFile.fileName}'?", "Confirm Download", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirmResult == DialogResult.Yes)
                    {
                        using (SaveFileDialog saveFileDialog = new SaveFileDialog
                        {
                            FileName = selectedFile.fileName,
                            Filter = "xDelta Files (*.xdelta)|*.xdelta",
                            Title = "Save xDelta File"
                        })
                        {
                            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                            {
                                string savePath = saveFileDialog.FileName;
                                await DownloadFileAsync(selectedFile.fileName, savePath);
                                txtPatchingApplicationXdeltaPath.Text = savePath;
                            }
                        }
                    }
                }
            }
        }

        private async Task DownloadFileAsync(string fileName, string savePath)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync($"xdelta/download/{Uri.EscapeDataString(fileName)}");
                if (response.IsSuccessStatusCode)
                {
                    byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(savePath, fileBytes);
                    MessageBox.Show("File downloaded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    MessageBox.Show("File not found on the server.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    MessageBox.Show("Session expired. Please authenticate again.", "Session Expired", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    using (AuthForm authForm = new AuthForm(this))
                    {
                        if (authForm.ShowDialog() == DialogResult.OK)
                        {
                            await DownloadFileAsync(fileName, savePath); // Retry download
                        }
                    }
                }
                else
                {
                    string errorMsg = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Download failed: {errorMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 5. Data Model
        private class XDeltaFile
        {
            public string mod { get; set; }
            public string fileName { get; set; }
            public long fileSize { get; set; }
            public DateTime uploadedDate { get; set; }
        }
    }
}

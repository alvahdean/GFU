using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace GFU.Razor.Lib
{
    public class GfuUtil
    {
        #region Constants
        public const string DefaultIp = "192.168.0.111";
        public const string DefaultUserName = "admin";
        public const string DefaultPassword = "";
        #endregion

        #region Internal instance properties/fields
        private object syncRoot;
        private bool isLocked;
        private HttpClient client;
        #endregion


        #region Instance management
        public GfuUtil(string ipAddr=null,string user=null,string pass=null)
        {
            syncRoot = new object();
            isLocked = false;

            if (String.IsNullOrWhiteSpace(ipAddr))
                ipAddr = DefaultIp;
            if (String.IsNullOrWhiteSpace(user))
                user = DefaultUserName;
            if (String.IsNullOrWhiteSpace(pass))
                pass = DefaultPassword;

            Address = IPAddress.Parse(ipAddr.Trim());
            UserName = user?.Trim();
            Password = pass?.Trim();
        }
        #endregion

        #region Public instance properties
        public bool IsLocked { get { return isLocked; } }
        public IPAddress Address { get; protected set; }
        public string UserName { get; protected set; }
        public string Password { get; protected set; }
        #endregion

        #region Internal methods
        protected HttpClient GetClient()
        {
            NetworkCredential cred = new NetworkCredential(UserName, Password);
            HttpClient cli = new HttpClient(MsgHandler());
            HttpHost targetHost = new HttpHost("localhost", 8080, "http");
            CredentialsProvider credsProvider = new BasicCredentialsProvider();
            credsProvider.setCredentials(AuthScope.ANY,
              new UsernamePasswordCredentials(DEFAULT_USER, DEFAULT_PASS));

            AuthCache authCache = new BasicAuthCache();
            authCache.put(targetHost, new BasicScheme());

            // Add AuthCache to the execution context
            final HttpClientContext context = HttpClientContext.create();
            context.setCredentialsProvider(credsProvider);
            context.setAuthCache(authCache);

            //WebRequest req = WebRequest.Create("http://" + Address.ToString() + "/en/" + f);
            //req.Credentials = new NetworkCredential(UserName, Password);
        }
        public HttpMessageHandler MsgHandler()
        {

        }
        #endregion

        #region Controller Web Requests
        private bool HttpPost(string f, string s)
        {
            WebRequest req = WebRequest.Create("http://" + Address.ToString() + "/en/" + f);
            req.Credentials = new NetworkCredential(UserName, Password);
            req.Timeout = 10000; // don't worry about the timeout here
            req.Method = "POST";

            string postData = s;
            byte[] byteArray = Encoding.UTF8.GetBytes(postData);
            req.ContentType = "application/x-www-form-urlencoded";
            req.ContentLength = byteArray.Length;
            Stream dataStream = req.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            WebResponse res = req.GetResponse();
            StreamReader sr = new StreamReader(res.GetResponseStream());
            string returnvalue = sr.ReadToEnd();
            return true;
        }

        private bool Submit(string f, string s)
        {
            WebRequest req = WebRequest.Create("http://" + Address.ToString() + "/en/" + f + "?" + s);
            req.Credentials = new NetworkCredential(UserName, Password);
            req.Timeout = 1000; // don't worry about the timeout here

            req.Method = "GET";
            WebResponse res = req.GetResponse();
            StreamReader sr = new StreamReader(res.GetResponseStream());
            string returnvalue = sr.ReadToEnd();
            return true;
        }

        private string HttpGet(string f, string s)
        {
            WebRequest req = WebRequest.Create("http://" + Address.ToString() + "/en/" + f + "?" + s);
            req.Credentials = new NetworkCredential(UserName, Password);
            req.Timeout = 40000;

            req.Method = "GET";
            WebResponse res = req.GetResponse();
            StreamReader sr = new StreamReader(res.GetResponseStream());
            string returnvalue = sr.ReadToEnd();
            return returnvalue;
        }

        private bool HttpDelete(string f, string s)
        {
            // turns out delete doesn't work the first time on 2012 firmware. Need to try it again
            // and then it works
            for (int i = 0; i < 3; ++i)
                try
                {
                    WebRequest request = WebRequest.Create("ftp://" + Address.ToString() + "/" + f);
                    request.Method = WebRequestMethods.Ftp.DeleteFile;
                    request.Credentials = new NetworkCredential(UserName, Password);
                    request.Timeout = 5000;
                    using (var resp = (FtpWebResponse)request.GetResponse())
                    {
                        Console.WriteLine(resp.StatusCode);
                    }
                    return true;

                }
                catch (Exception ex)
                {
                    if (i < 2)
                    {
                        System.Threading.Thread.Sleep(500); //retry
                        continue;
                    }
                    if (ex is WebException)
                    {
                        if ((ex as WebException).Status == WebExceptionStatus.ConnectFailure)
                        {
                            //Update with IProgress
                            //UploadState.Message = "Failed to connect to Gemini:\n\nError: " + (ex as WebException).Message+ "\nCouldn't delete a file on Gemini SD card: " + f;
                            //UploadState.Progress = 0;
                        }
                        continue;
                    }
                    return false;
                }

            return true;
        }

        #endregion

        #region GFU Utilities
        private int UploadCount = 0;
        private string CombinedFile = "";
        private int totalFiles = 0;
        private System.Timers.Timer tmTimer = new System.Timers.Timer();
        private bool CheckVersion(IProgress<GFUTaskProgress> progress = null)
        {
            GFUTaskProgress prg = new GFUTaskProgress(5, "Initializing...", GFUTaskState.Running);
            if (progress != null)
                progress.Report(new GFUTaskProgress(prg));
            try
            {
                string s = "";
                try
                {
                    prg.Message = "Sending controller request...";
                    if (progress != null)
                        progress.Report(new GFUTaskProgress(prg));
                    s = HttpGet("firmware.cgi", "");
                }
                catch (WebException ex)
                {
                    // if file not found, perhaps it's just a clean SD card
                    if (!ex.Message.Contains("404"))
                        throw ex;
                }

                int idx = s.IndexOf("Build date:");
                if (idx < 0)
                {
                    //DialogResult res = MessageBox.Show(this, "Please note that if your current Gemini firmware version is earlier than Dec 18, 2012, you should first upgrade to Dec 18, 2012 firmware before proceeding!\n\nDo you want to continue anyway (not recommended)?", "Version check", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    //if (res != DialogResult.Yes)
                    //{
                    //    return false;
                    //}
                    //else
                    prg.Message = "No build date found, ok";
                    if (progress != null)
                        progress.Report(new GFUTaskProgress(prg));
                    return true;
                }

                try
                {
                    string[] sa = (s.Substring(idx)).Split(new string[] { "<BR>" }, StringSplitOptions.RemoveEmptyEntries);
                    string v = "Build date:";
                    s = sa[0].Trim().Substring(v.Length);
                    s = s.Trim();
                    DateTime dt = new DateTime();

                    if (DateTime.TryParse(s, out dt))
                    {
                        _previousDateTime = dt.ToLongDateString();

                        //if (dt < new DateTime(2012, 12, 17))
                        //{

                        //    DialogResult res = MessageBox.Show(this, "Your firmware version (" + previousDateTime + ") is earlier than\nDecember 18, 2012.\n\nYou should first upgrade to December 18, 2012 firmware before proceeding!\n\nDo you want to continue anyway (not recommended)?", "Version check", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Hand, MessageBoxDefaultButton.Button2);
                        //    if (res != DialogResult.Yes)
                        //    {
                        //        return false;
                        //    }
                        //}
                        prg.Message = $"Build date: {_previousDateTime}";
                        prg.PercentComplete += 5;
                        if (progress != null)
                            progress.Report(new GFUTaskProgress(prg));

                    }

                }
                catch (Exception ex)
                {
                    //DialogResult res = MessageBox.Show(this, "Please note that if your firmware version is earlier than December 18, 2012, you should first upgrade to Dec 18, 2012 firmware before proceeding!\n\nDo you want to continue anyway (not recommended)?", "Version check", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    //if (res != DialogResult.Yes)
                    //{
                    //    return false;
                    //}
                    prg.Message = $"Ignoring error: {ex.Message}";
                    if (progress != null)
                        progress.Report(new GFUTaskProgress(prg));
                }
                return true;
            }
            catch (Exception ex)
            {
                prg.Exception = ex;
                prg.Message = "Failed to connect to Gemini. Please check that it's connected, turned ON, and at the correct IP address.\n"
                    + prg.Message;
                if (progress != null)
                    progress.Report(new GFUTaskProgress(prg));
                return false;
            }
        }

        private bool ftpAll(string fromPath, string to, string uname, string pwd, IProgress<GFUTaskProgress> progress = null)
        {
            //if (bError) return false;

            string[] dirs = Directory.GetDirectories(fromPath);

            foreach (string d in dirs)
            {
                //if (bError) return false;

                string p = Path.GetFileName(d);

                try
                {
                    WebRequest request = WebRequest.Create("ftp://" + to + "/" + p);
                    request.Method = WebRequestMethods.Ftp.MakeDirectory;
                    request.Credentials = new NetworkCredential(uname, pwd);
                    request.Timeout = 5000;
                    using (var resp = (FtpWebResponse)request.GetResponse())
                    {
                        Console.WriteLine(resp.StatusCode);
                    }

                }
                catch (Exception ex)
                {

                    if (ex is WebException)
                    {
                        if ((ex as WebException).Status == WebExceptionStatus.ConnectFailure)
                        {

                            //UploadState.Progress = 0;
                            _lastError = "Failed to connect to Gemini: " + (ex as WebException).Message;
                            return false;
                        }

                    }

                }

                string dirPath = Path.Combine(fromPath, p);
                string toPath = to + "/" + p;

                ftpAll(dirPath, toPath, uname, pwd);

                //string[] files = Directory.GetFiles(dirPath);
                //foreach (string f in files)
                //{
                //    string fname = Path.GetFileName(f);
                //    using (WebClient webClient = new WebClient())
                //    {
                //        webClient.Credentials = new NetworkCredential(uname, pwd);
                //        webClient.UploadFile("ftp://" + toPath + "/" + fname, f);
                //    }
                //}
            }



#if false
            {
                try
                {
                    string[] files2 = Directory.GetFiles(fromPath);

                    EnterpriseDT.Net.Ftp.FTPConnection ftpConnection = new FTPConnection();
                    ftpConnection.ServerAddress =  txtIP.Text;
                    ftpConnection.UserName = uname;
                    ftpConnection.Password = "aa";
                    ftpConnection.AccountInfo = "";

                    ftpConnection.Connect();

                    string x = to.Replace(txtIP.Text, "");
                    if (x.StartsWith("/")) x = x.Substring(1);

                    ftpConnection.ChangeWorkingDirectory(x);

                    foreach (string f in files2)
                    {

                        if (bError) return false;

                        string fname = Path.GetFileName(f);

                        try
                        {
                            ftpConnection.UploadFile(f, fname);
                        }
                        catch (Exception ex)
                        {
                            if (!bError)
                            {
                                bError = true;
                                bCancel = true;
                                MessageBox.Show(this, ex.Message + "\n" + fname + "\n\nDetails:\n" + ex.ToString(), "Failed to upload", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        progUpload.Value = (int)((1000 * UploadCount) / totalFiles);
                        if (!bError)
                        {
                            UploadCount++;
                            lbUploadPercent.Text = (progUpload.Value / 10).ToString() + "%" + " (" + UploadCount.ToString() + "/" + totalFiles.ToString() + ")";
                            Application.DoEvents();
                        }
                    }

                    ftpConnection.Close();

                }
                catch (Exception ex)
                {
                  
                }
            }
//#else

            {
                try
                {
                    string[] files2 = Directory.GetFiles(fromPath);

                    foreach (string f in files2)
                    {

                        if (bError) return false;

                        string fname = Path.GetFileName(f);

                        System.Threading.ThreadPool.QueueUserWorkItem(arg =>
                        {
                            semConn.WaitOne();


                            using (MyWebClient webClient = new MyWebClient())
                            {
                                webClient.Credentials = new NetworkCredential(uname, pwd);
              
                                try
                                {
                                    webClient.UploadFile("ftp://" + to + "/" + fname, f);
                                    while (webClient.IsBusy)
                                        System.Threading.Thread.Sleep(100);
                                }
                                catch (Exception ex)
                                {
                                    lock (this)
                                        this.Invoke(new Action(() =>
                                        {
                                            if (!bError)
                                            {
                                                bError = true;
                                                bCancel = true;
                                                MessageBox.Show(this,
                                                    ex.Message + "\n" + fname + "\n\nDetails:\n" + ex.ToString(),
                                                    "Failed to upload", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                        }));
                                }

                                this.Invoke(new Action(() =>
                                {
                                    progUpload.Value = (int) ((1000*UploadCount)/totalFiles);
                                    if (!bError)
                                    {
                                        UploadCount++;
                                        lbUploadPercent.Text = (progUpload.Value/10).ToString() + "%" + " (" +
                                                               UploadCount.ToString() + "/" + totalFiles.ToString() +
                                                               ")";
                                        Application.DoEvents();

                                    }
                                }));

                            }
                            semConn.Release();
                        });


                    }
                }
                catch (Exception ex)
                {

                }
            }
            return true;
#endif


            using (WebClient webClient = new WebClient())
            {
                webClient.Credentials = new NetworkCredential(uname, pwd);

                string[] files2 = Directory.GetFiles(fromPath);
                foreach (string f in files2)
                {
                    string fname = Path.GetFileName(f);
                    try
                    {
                        webClient.UploadFile("ftp://" + to + "/" + fname, f);
                        while (webClient.IsBusy)
                            System.Threading.Thread.Sleep(100);
                    }
                    catch (Exception ex)
                    {
                        _lastError = ex.Message + "\n\n" + to + "/" + fname + "\n\nDetails:\n" + ex.ToString();
                        return false;
                    }
                    try
                    {
                        //UploadState.Progress = UploadCount / totalFiles *100;
                    }
                    catch
                    {
                    }
                    //Application.DoEvents();
                    UploadCount++;
                }
            }

            return true;
        }


        private bool Reboot(IProgress<GFUTaskProgress> progress = null)
        {
            bool result = false;
            GFUTaskProgress st = new GFUTaskProgress(10, "Issuing reboot command...", GFUTaskState.Running);
            progress?.Report(new GFUTaskProgress(st));
            try
            {
                Submit("firmware.cgi", "bC=Cold Reboot");
            }
            catch (Exception ex)
            {
                st.Message += $"\nIgnoring request error:{ex.Message}\n";
            }
            st.Message = "\nReboot in progress...";
            st.PercentComplete = 50;
            progress?.Report(new GFUTaskProgress(st));

            result = WaitForGemini("Reboot");
            st.State = result ? GFUTaskState.Success : GFUTaskState.Failed;
            st.Message += result ? "\nReboot successful" : "\nReboot failed";
            progress?.Report(new GFUTaskProgress(st));
            return result;
        }

        private bool ResetSRAM()
        {
            try
            {
                Submit("firmware.cgi", "CL=RESET SRAM");
            }
            catch
            {
            }
            return WaitForGemini("SRAM Reset");
        }

        private bool FlashFirmware(string fname)
        {
            try
            {
                Submit("index.cgi", "ff=" + fname);
            }
            catch
            {
            }
            return WaitForGemini("Flash Firmware");
        }

        private bool WaitForGemini(string msg)
        {
            try
            {
                string res = HttpGet("firmware.cgi", "");
                return true;
            }
            catch (Exception ex)
            {
                _lastError = "Timeout occurred while waiting for Gemini to " + msg + "\n\nDetails:\n" + ex.ToString() + "\n"
                    + "Failed to " + msg;
                return false;
            }
        }

        #endregion
    }
}

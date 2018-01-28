using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using GFU.Razor.Lib;

namespace GFU.Razor.Pages
{

    public class GFUModel : PageModel
    {
        private string _lastError = "";
        private string _previousDateTime = "(unknown)";

        public UpdateGFUTask CurrentTask { get; set; } = UpdateGFUTask.Initialize;
        [Display(Name ="Gemini IP Address:")]
        public IPAddress Address { get; set; } = new IPAddress(new byte[] { 192, 168, 0, 111 });
        [Display(Name = "Username:")]
        public string UserName { get; set; } = "admin";
        [Display(Name = "Password:")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Gemini")]
        public bool UpdateGemini { get; set; } = true;
        [Display(Name = "Hand Controller")]
        public bool UpdateHC { get; set; } = true;
        [Display(Name = "Videos")]
        public bool UpdateVideos { get; set; } = false;
        [Display(Name = "Catalogs")]
        public bool UpdateCatalogs { get; set; } = false;
        [Display(Name = "Firmware")]
        public bool UpdateFlashFirmware { get; set; } = false;
        [Display(Name = "Firmware Url")]
        public string FirmwareUrl { get; set; } = "http://www.gemini-2.com/firmware1/current/combined.zip";

        public int InduceTestFailAt { get; set; } = -1;

        public GFUTaskProgress TestState { get; set; } = new GFUTaskProgress();
        public GFUTaskProgress ConnectState { get; set; } = new GFUTaskProgress();
        public GFUTaskProgress DownloadState { get; set; } = new GFUTaskProgress();
        public GFUTaskProgress ExtractState { get; set; } = new GFUTaskProgress();
        public GFUTaskProgress UploadState { get; set; } = new GFUTaskProgress();
        public GFUTaskProgress FlashState { get; set; } = new GFUTaskProgress();
        public GFUTaskProgress ResetSRAMState { get; set; } = new GFUTaskProgress();
        public GFUTaskProgress RebootState { get; set; } = new GFUTaskProgress();
        public GFUTaskProgress FormatSDState { get; set; } = new GFUTaskProgress();

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostTestAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            
            // The Progress<T> constructor captures our UI context,
            //  so the lambda will be run on the UI thread.
            var progress = new Progress<GFUTaskProgress>(prg =>
            {
                TestState = prg;
//                if(TestState.State==GFUTaskState.Running)

            });
            
            bool result=await Task.Run(() => TestWork(progress,InduceTestFailAt));
            if (result)
            {
                ViewData["TestState.State"] = GFUTaskState.Success;
                ViewData["TestState.Message"] = "Test workload completed successfully";
            }
            else
            {
                ViewData["TestState.State"] = GFUTaskState.Failed;
            }
            return Page();
        }
        private bool TestWork(IProgress<GFUTaskProgress> progress = null,int failAt=-1)
        {
            GFUTaskProgress st = new GFUTaskProgress(0, "Starting test workload", GFUTaskState.Running);
            progress?.Report(new GFUTaskProgress(st));
            for (int i = 1; i <= 100; ++i)
            {
                try
                {
                    Thread.Sleep(100); // CPU-bound work
                    if (i == failAt)
                        throw new TimeoutException($"WorkItem[{i}]: Induced timeout exception");
                    if ((i % 10) == 0)
                        st.Message += $"\nWorkItem[{i}] completed";
                    st.PercentComplete++;
                }
                catch (Exception ex)
                {
                    st.Exception = ex;
                    progress?.Report(new GFUTaskProgress(st));
                    return false;
                }
                progress?.Report(new GFUTaskProgress(st));
            }
            st.State = GFUTaskState.Success;
            st.Message += "\nTest workload success!";
            return true;
        }
        public async Task<IActionResult> OnPostStartAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            // The Progress<T> constructor captures our UI context,
            //  so the lambda will be run on the UI thread.
            var progress = new Progress<GFUTaskProgress>(prg =>
            {
                ConnectState = prg;
            });

            // DoProcessing is run on the thread pool.
            bool result=await Task.Run(() => CheckVersion(progress));
            if(result)
            {
                ViewData["StartState.State"] = GFUTaskState.Success;
                ViewData["StartState.Message"]="Version check completed successfully";
            }
            else
            {
                ViewData["StartState.State"] = GFUTaskState.Failed;
            }
            return Page();
        }


    }
}
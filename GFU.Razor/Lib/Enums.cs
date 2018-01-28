using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GFU.Razor.Lib
{
    public enum UpdateGFUTask
    {
        Initialize = 0,
        Start = 10,
        Download = 20,
        Extract = 30,
        Upload = 40,
        Flash = 50,
        ResetSRAM = 55,
        Reboot = 60,
        Complete = 70
    }

    [Flags]
    public enum UpdateItems
    {
        Gemini = 1,
        HC = 2,
        FlashFirmware = 4,
        Videos = 8,
        Catalogs = 16
    }

    public enum GFUTaskState
    {
        Unspecified = 0,
        Pending = 10,
        Running = 20,
        Success = 30,
        Failed = 100
    }

    public struct GFUTaskProgress
    {
        private int _percentComplete;
        private string _message;
        private GFUTaskState _state;
        private Exception _exception;

        public GFUTaskProgress(GFUTaskState state)
        {
            _percentComplete = 0;
            _message = String.Empty;
            _exception = null;
            _state = GFUTaskState.Pending;
            State = state;
        }
        public GFUTaskProgress(Exception ex, int pct = -1)
        {
            _percentComplete = 0;
            _message = String.Empty;
            _state = GFUTaskState.Pending;
            _exception = null;
            PercentComplete = pct;
            Exception = ex;
            Message = ex?.Message?.Trim() ?? "";
            State = ex != null ? GFUTaskState.Failed : GFUTaskState.Unspecified;
        }
        public GFUTaskProgress(int pct, string msg = null, GFUTaskState state = GFUTaskState.Pending)
        {
            _percentComplete = 0;
            _message = String.Empty;
            _state = GFUTaskState.Pending;
            _exception = null;
            PercentComplete = pct;
            Message = msg?.Trim() ?? "";
            State = state;
            Exception = null;
        }
        public GFUTaskProgress(GFUTaskProgress other)
        {
            _percentComplete = other.PercentComplete;
            _message = other.Message;
            _state = other.State;
            _exception = other.Exception;
        }

        public int PercentComplete
        {
            get { return _percentComplete; }
            set
            {
                if (value >= 0 && value <= 100)
                    _percentComplete = value;
            }
        }
        public string Message
        {
            get { return _message; }
            set { if (!String.IsNullOrWhiteSpace(value)) _message = value.Trim(); }
        }
        public GFUTaskState State
        {
            get { return _state; }
            set
            {
                if (value != GFUTaskState.Unspecified) { _state = value; }
                if (State == GFUTaskState.Success && PercentComplete < 100)
                {
                    Message = "Completed successfully";
                    PercentComplete = 100;
                    Exception = null;
                }
            }
        }
        public Exception Exception
        {
            get { return _exception; }
            set
            {
                if (value != null)
                {
                    _exception = value;
                    State = GFUTaskState.Failed;
                    Message = $"[{value.GetType().Name}]: {value.Message}";
                }
            }
        }
    }
}

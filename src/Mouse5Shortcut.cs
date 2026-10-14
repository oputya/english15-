using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// The hook thread only filters input and posts a notification. Capture/API work stays on the UI thread.
sealed class Mouse5Shortcut : IDisposable {
    public const int ActionMessage=0x8000+51;
    delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct MouseData { public int x,y; public uint mouseData,flags,time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct NativeMessage { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public int x,y; public uint privateData; }
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,UIntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern int GetMessage(out NativeMessage message,IntPtr hwnd,uint min,uint max);
    [DllImport("user32.dll")] static extern bool PeekMessage(out NativeMessage message,IntPtr hwnd,uint min,uint max,uint remove);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    readonly Thread thread; readonly ManualResetEvent ready=new ManualResetEvent(false);
    HookProc callback; IntPtr hook,target; uint threadId; Exception startupError;
    volatile bool enabled,paused,stopping; bool pressed; int queued;
    public bool Enabled { get { return enabled; } set { enabled=value; } }
    public bool Paused { get { return paused; } set { paused=value; } }
    public IntPtr Target { get { return Interlocked.CompareExchange(ref target,IntPtr.Zero,IntPtr.Zero); } set { Interlocked.Exchange(ref target,value); } }
    public void Acknowledge() { Interlocked.Exchange(ref queued,0); }
    public Mouse5Shortcut(IntPtr owner) {
        Target=owner; thread=new Thread(Run) { IsBackground=true,Name="Mouse shortcut" }; thread.Start();
        ready.WaitOne(); ready.Dispose();
        if(startupError!=null) throw startupError;
    }
    void Run() {
        try {
            threadId=GetCurrentThreadId(); NativeMessage initial; PeekMessage(out initial,IntPtr.Zero,0,0,0);
            callback=Filter; hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);
            if(hook==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        } catch(Exception ex) { startupError=ex; }
        finally { ready.Set(); }
        if(startupError!=null) return;
        try { NativeMessage message; while(!stopping && GetMessage(out message,IntPtr.Zero,0,0)>0) { } }
        finally { if(hook!=IntPtr.Zero) UnhookWindowsHookEx(hook); hook=IntPtr.Zero; GC.KeepAlive(callback); }
    }
    IntPtr Filter(int code,IntPtr message,IntPtr data) {
        if(code>=0 && !stopping && (message.ToInt64()==0x20B || message.ToInt64()==0x20C)) {
            var input=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));
            if((input.mouseData>>16)==2) {
                if(message.ToInt64()==0x20B) {
                    if(pressed) return new IntPtr(1);
                    if(enabled) {
                        pressed=true;
                        if(!paused && Interlocked.CompareExchange(ref queued,1,0)==0) {
                            if(!PostMessage(Target,ActionMessage,IntPtr.Zero,IntPtr.Zero)) Acknowledge();
                        }
                        return new IntPtr(1);
                    }
                } else if(pressed) { pressed=false; return new IntPtr(1); }
            }
        }
        return CallNextHookEx(hook,code,message,data);
    }
    public void Dispose() {
        if(stopping) return; enabled=false; stopping=true; Target=IntPtr.Zero;
        PostThreadMessage(threadId,0x12,UIntPtr.Zero,IntPtr.Zero); thread.Join(1000);
    }
}

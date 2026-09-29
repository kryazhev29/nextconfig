param([Parameter(Mandatory=$true)][int]$DemoPid,[int]$Direction=7,[switch]$List)
Add-Type @"
using System;using System.Text;using System.Collections.Generic;using System.Runtime.InteropServices;
public class ScrollTarget {
 public delegate bool EnumProc(IntPtr h,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc f,IntPtr p);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr h,EnumProc f,IntPtr p);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int index);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out R r);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 [StructLayout(LayoutKind.Sequential)]public struct R {public int L,T,Ri,B;}
 public static string Run(uint pid,int direction,bool list) {
  IntPtr best=IntPtr.Zero;int area=0;string result="";
  EnumWindows((w,x)=>{uint p;GetWindowThreadProcessId(w,out p);if(p!=pid||!IsWindowVisible(w))return true;
   EnumChildWindows(w,(h,y)=>{if(!IsWindowVisible(h))return true;int style=GetWindowLong(h,-16);R r;GetWindowRect(h,out r);
    if((style&0x100000)!=0)result+="HSCROLL "+h+" ";
    if((style&0x200000)!=0){int a=(r.Ri-r.L)*(r.B-r.T);if(a>area){area=a;best=h;}}
    return true;},IntPtr.Zero);return true;},IntPtr.Zero);
  if(!list&&best!=IntPtr.Zero)SendMessage(best,0x115,(IntPtr)direction,IntPtr.Zero);
  return result+"target="+best;
 }
}
"@
$p=Get-Process NextConfig -ErrorAction Stop
if(@($p).Count -ne 1 -or $p.Id -ne $DemoPid){throw 'Expected one owned demo'}
[ScrollTarget]::Run([uint32]$p.Id,$Direction,[bool]$List)

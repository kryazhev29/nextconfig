# -Caption: вместо прокрутки в $Direction листает страницу сверху, пока окно с этим текстом
# (подпись карточки) не окажется у верхнего края видимой части.
param([Parameter(Mandatory=$true)][int]$DemoPid,[int]$Direction=7,[switch]$List,[string]$Caption='')
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
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr h,StringBuilder s,int max);
 [DllImport("user32.dll")]static extern int GetScrollPos(IntPtr h,int bar);
 [StructLayout(LayoutKind.Sequential)]public struct R {public int L,T,Ri,B;}
 public static string Run(uint pid,int direction,bool list,string caption) {
  IntPtr best=IntPtr.Zero,mark=IntPtr.Zero;int area=0;string result="";
  EnumWindows((w,x)=>{uint p;GetWindowThreadProcessId(w,out p);if(p!=pid||!IsWindowVisible(w))return true;
   EnumChildWindows(w,(h,y)=>{if(!IsWindowVisible(h))return true;int style=GetWindowLong(h,-16);R r;GetWindowRect(h,out r);
    if((style&0x100000)!=0)result+="HSCROLL "+h+" ";
    if((style&0x200000)!=0){int a=(r.Ri-r.L)*(r.B-r.T);if(a>area){area=a;best=h;}}
    if(caption!=""&&mark==IntPtr.Zero){StringBuilder s=new StringBuilder(256);GetWindowText(h,s,256);if(s.ToString()==caption)mark=h;}
    return true;},IntPtr.Zero);return true;},IntPtr.Zero);
  if(list||best==IntPtr.Zero)return result+"target="+best;
  if(caption==""){SendMessage(best,0x115,(IntPtr)direction,IntPtr.Zero);return result+"target="+best;}
  if(mark==IntPtr.Zero)return "caption not found: "+caption;
  // SB_TOP, затем SB_PAGEDOWN, пока подпись ниже видимой части, и SB_LINEDOWN до верхнего края.
  SendMessage(best,0x115,(IntPtr)6,IntPtr.Zero);
  for(int i=0;i<2000;i++){
   R view,item;GetWindowRect(best,out view);GetWindowRect(mark,out item);
   int before=GetScrollPos(best,1);
   if(item.T>=view.B)SendMessage(best,0x115,(IntPtr)3,IntPtr.Zero);
   else if(item.T>view.T+12)SendMessage(best,0x115,(IntPtr)1,IntPtr.Zero);
   else break;
   if(GetScrollPos(best,1)==before)break;
  }
  return "shown "+caption+" target="+best;
 }
}
"@
$p=Get-Process NextConfig -ErrorAction Stop
if(@($p).Count -ne 1 -or $p.Id -ne $DemoPid){throw 'Expected one owned demo'}
$result=[ScrollTarget]::Run([uint32]$p.Id,$Direction,[bool]$List,$Caption)
if($Caption -and -not $List -and $result -notlike 'shown *'){throw $result}
$result

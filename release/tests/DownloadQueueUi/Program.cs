using System.Collections;
using System.Reflection;
using VideoEnhancer.Testing;
using System.Windows.Forms;
class Program {
 static BindingFlags f=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 static object Field(object o,string n)=>o.GetType().GetField(n,f)!.GetValue(o)!;
 static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,f)!.Invoke(o,a)!;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static void Wait(Func<bool> done){var end=DateTime.UtcNow.AddSeconds(20);while(!done()){Application.DoEvents();Thread.Sleep(10);if(DateTime.UtcNow>end)throw new Exception("UI probe timeout");}Application.DoEvents();}
 [STAThread] static int Main(string[] args){
  if(args.Length > 0 && args[0].StartsWith("--"))return Child(args);
  var root=RepositoryPaths.ResolveRoot(args.FirstOrDefault());var probe=Path.Combine(root,"Artifacts/.refactor-tmp/download-queue-ui/fixture");
  var plugin=Path.Combine(probe,"Plugin");var core=Path.Combine(plugin,"videoenhancer");Directory.CreateDirectory(core);
  File.Copy(Path.Combine(root,"VideoEnhancerPlugin/obj/plugin-artifact/videoenhancer.3fui.dll"),Path.Combine(plugin,"videoenhancer.3fui.dll"),true);
  foreach(var file in Directory.GetFiles(AppContext.BaseDirectory))File.Copy(file,Path.Combine(core,Path.GetFileName(file)),true);
  // 使用测试项目的 apphost，兼容 dotnet run 和 dotnet Probe.dll 两种启动方式。
  File.Copy(Path.ChangeExtension(Assembly.GetExecutingAssembly().Location,".exe"),Path.Combine(core,"videoenhancer.exe"),true);
  var asm=Assembly.LoadFrom(Path.Combine(plugin,"videoenhancer.3fui.dll"));
  foreach(var mode in new[]{"success","failure","cancel","stop-install"}){
   var trace=Path.Combine(probe,mode+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(trace);
   Environment.SetEnvironmentVariable("QUEUE_PROBE_TRACE",trace);Environment.SetEnvironmentVariable("QUEUE_PROBE_MODE",mode);
   var cfg=Activator.CreateInstance(asm.GetType("videoenhancer.PluginConfig")!)!;
   using var panel=(Control)Activator.CreateInstance(asm.GetType("videoenhancer.PluginPanel")!,cfg,true)!;var handle=panel.Handle;
   Call(panel,"RenderDownloadModels","[{\"name\":\"backend\",\"path\":\"Backend/probe.7z\",\"size\":1},{\"name\":\"model\",\"path\":\"PTH/probe.pth\",\"size\":1}]","",0,"{\"state\":\"update-available\",\"installedVersion\":\"1\",\"latestVersion\":\"2\",\"mode\":\"patch\",\"downloadSize\":1,\"fullSize\":1}",0);
   Call(panel,"OnDownloadAllClick",panel,EventArgs.Empty);
   Wait(()=>File.Exists(Path.Combine(trace,"backend.started")));
   Check(!File.Exists(Path.Combine(trace,"model.started")),mode+": models wait for backend");
   var button=Field(panel,"_btnDownloadPluginUpdate");Check((string)button.GetType().GetProperty("Text")!.GetValue(button)! == "停止全部","download-all becomes stop-all");
   if(mode=="cancel"){
    var request=((IDictionary)Field(panel,"_downloadCancellations"))["Backend/probe.7z"]!;Call(request,"Cancel");
   }else if(mode=="stop-install"){
    Wait(()=>{var request=((IDictionary)Field(panel,"_downloadCancellations"))["Backend/probe.7z"];return request!=null&&(bool)request.GetType().GetProperty("Installing",f)!.GetValue(request)!;});
    Call(panel,"OnDownloadAllClick",panel,EventArgs.Empty);
    var request=((IDictionary)Field(panel,"_downloadCancellations"))["Backend/probe.7z"]!;
    Check(!(bool)request.GetType().GetProperty("Requested",f)!.GetValue(request)!,"stop-all preserves backend transaction");
    Check((bool)Field(panel,"_downloadAllBusy"),"stop-all waits for transaction");
    File.WriteAllText(Path.Combine(trace,"release"),"");
   }else File.WriteAllText(Path.Combine(trace,"release"),"");
   Wait(()=>!(bool)Field(panel,"_downloadAllBusy"));
   Check(File.Exists(Path.Combine(trace,"model.started"))==(mode=="success"),mode+": backend gates model queue");
   Check((string)button.GetType().GetProperty("Text")!.GetValue(button)! == "下载全部","button resets after queue");
  }
  var retryTrace=Path.Combine(probe,"retry-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(retryTrace);
  Environment.SetEnvironmentVariable("QUEUE_PROBE_TRACE",retryTrace);Environment.SetEnvironmentVariable("QUEUE_PROBE_MODE","retry");
  var retryCfg=Activator.CreateInstance(asm.GetType("videoenhancer.PluginConfig")!)!;
  using(var panel=(Control)Activator.CreateInstance(asm.GetType("videoenhancer.PluginPanel")!,retryCfg,true)!){
   var handle=panel.Handle;
   var models=System.Text.Json.JsonSerializer.Serialize(Enumerable.Range(1,5).Select(n=>new{name=n.ToString(),path=$"PTH/{n}.pth",size=1}));
   Call(panel,"RenderDownloadModels",models,"",0,"",-1);
   Call(panel,"OnDownloadAllClick",panel,EventArgs.Empty);
   Wait(()=>Directory.GetFiles(retryTrace,"started-*").Length==3);
   var item=((IDictionary)Field(panel,"_downloadItemsByPath"))["PTH/1.pth"]!;
   var eventType=item.GetType().DeclaringType!.GetNestedType("ListItemEventArgs",f)!;
   var click=Activator.CreateInstance(eventType,item,1,3)!;
   string ActionText(){var items=(IList)item.GetType().GetProperty("SubItems")!.GetValue(item)!;return (string)items[3]!.GetType().GetProperty("Text")!.GetValue(items[3])!;}
   Call(panel,"OnDownloadListItemClick",panel,click);
   Wait(()=>ActionText()=="重试"&&Directory.GetFiles(retryTrace,"started-*").Length==4);
   Call(panel,"OnDownloadListItemClick",panel,click);
   Check(ActionText()=="排队中","retry joins active download-all queue");
   Call(panel,"OnDownloadListItemClick",panel,click);
   File.WriteAllText(Path.Combine(retryTrace,"release-all"),"");
   Wait(()=>!(bool)Field(panel,"_downloadAllBusy"));
   Check(Directory.GetFiles(retryTrace,"started-1-*").Length==2&&Directory.GetFiles(retryTrace,"started-*").Length==6,"UI retry runs exactly once after cancellation");
  }
  Console.WriteLine("DOWNLOAD_QUEUE_UI_PASS|backend-success|backend-failure|backend-cancel|stop-install|retry-enqueue");return 0;
 }
 static int Child(string[] args){
  var trace=Environment.GetEnvironmentVariable("QUEUE_PROBE_TRACE")!;var mode=Environment.GetEnvironmentVariable("QUEUE_PROBE_MODE")!;
  if(mode=="retry"){
   var id=Path.GetFileNameWithoutExtension(args[1]);File.WriteAllText(Path.Combine(trace,"started-"+id+"-"+Guid.NewGuid().ToString("N")),"");
   var cancel=Environment.GetEnvironmentVariable("VIDEOENHANCER_CANCEL_FILE");var deadline=DateTime.UtcNow.AddSeconds(20);
   while(!File.Exists(Path.Combine(trace,"release-all"))){if(File.Exists(cancel)){Console.Error.WriteLine("DOWNLOAD_CANCELLED|probe");return 1;}if(DateTime.UtcNow>deadline)return 2;Thread.Sleep(10);}
   return 0;
  }
  var backend=args[0]=="--update-backend";File.WriteAllText(Path.Combine(trace,backend?"backend.started":"model.started"),"");
  if(!backend)return 0;
  if(mode=="stop-install"){Console.WriteLine("BACKEND_INSTALL_START|安装中");Console.Out.Flush();}
  var marker=Environment.GetEnvironmentVariable("VIDEOENHANCER_CANCEL_FILE");
  var end=DateTime.UtcNow.AddSeconds(20);
  while(!File.Exists(Path.Combine(trace,"release"))){if(mode!="stop-install"&&File.Exists(marker)){Console.Error.WriteLine("DOWNLOAD_CANCELLED|probe");return 1;}if(DateTime.UtcNow>end)return 2;Thread.Sleep(10);}
  if(mode=="failure"){Console.Error.WriteLine("probe failure");return 1;}return 0;
 }
}

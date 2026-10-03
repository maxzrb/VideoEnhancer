Imports System
Imports System.Collections.Concurrent
Imports System.Globalization
Imports System.Reflection
Imports System.Runtime.ExceptionServices

Namespace videoenhancer

    ''' <summary>只访问进程内已加载的宿主；编译和加载插件均不要求外部宿主 DLL。</summary>
    Friend NotInheritable Class HostRuntime
        Friend Shared Property TestAssembly As Assembly
        Private Shared ReadOnly Members As New ConcurrentDictionary(Of (Type, String, Boolean), MemberInfo)()

        Private Sub New()
        End Sub

        Friend Shared Function GetHostAssembly() As Assembly
            If TestAssembly IsNot Nothing Then Return TestAssembly
            For Each candidate In AppDomain.CurrentDomain.GetAssemblies()
                If String.Equals(candidate.GetName().Name, "FFmpegFreeUI", StringComparison.OrdinalIgnoreCase) Then
                    Return candidate
                End If
            Next
            Return Nothing
        End Function

        Friend Shared Function ResolveType(name As String, Optional required As Boolean = True) As Type
            Dim host = GetHostAssembly()
            If host IsNot Nothing Then
                Dim result = host.GetType("FFmpegFreeUI." & name, False)
                If result Is Nothing Then result = host.GetType(name, False)
                If result IsNot Nothing Then Return result
            End If
            If required Then Throw New TypeLoadException("宿主缺少所需类型：" & name)
            Return Nothing
        End Function

        Private Shared Function ResolveMember(target As Object, name As String) As MemberInfo
            If target Is Nothing Then Throw New ArgumentNullException(NameOf(target))
            Dim sharedType = TryCast(target, Type)
            Dim targetType = If(sharedType, target.GetType())
            Dim isShared = sharedType IsNot Nothing
            Return Members.GetOrAdd((targetType, name, isShared),
                Function(key)
                    Dim flags = BindingFlags.Public Or BindingFlags.NonPublic Or
                        If(key.Item3, BindingFlags.Static Or BindingFlags.FlattenHierarchy, BindingFlags.Instance)
                    Dim member As MemberInfo = key.Item1.GetProperty(key.Item2, flags)
                    If member Is Nothing Then member = key.Item1.GetField(key.Item2, flags)
                    If member Is Nothing Then Throw New MissingMemberException(key.Item1.FullName, key.Item2)
                    Return member
                End Function)
        End Function

        Friend Shared Function ReadMember(target As Object, name As String) As Object
            Dim member = ResolveMember(target, name)
            Dim owner = If(TypeOf target Is Type, Nothing, target)
            Dim prop = TryCast(member, PropertyInfo)
            If prop IsNot Nothing Then Return prop.GetValue(owner)
            Return DirectCast(member, FieldInfo).GetValue(owner)
        End Function

        Friend Shared Function ReadValue(Of T)(target As Object, name As String) As T
            Dim value = ReadMember(target, name)
            If value Is Nothing Then Return Nothing
            If TypeOf value Is T Then Return DirectCast(value, T)
            Return DirectCast(Convert.ChangeType(value, GetType(T), CultureInfo.InvariantCulture), T)
        End Function

        Friend Shared Sub WriteMember(target As Object, name As String, value As Object)
            Dim member = ResolveMember(target, name)
            Dim owner = If(TypeOf target Is Type, Nothing, target)
            Dim prop = TryCast(member, PropertyInfo)
            Dim valueType = If(prop Is Nothing, DirectCast(member, FieldInfo).FieldType, prop.PropertyType)
            If value IsNot Nothing AndAlso Not valueType.IsInstanceOfType(value) Then
                value = Convert.ChangeType(value, valueType, CultureInfo.InvariantCulture)
            End If
            If prop IsNot Nothing Then
                prop.SetValue(owner, value)
            Else
                DirectCast(member, FieldInfo).SetValue(owner, value)
            End If
        End Sub

        Friend Shared Function InvokeShared(typeName As String, methodName As String, ParamArray args As Object()) As Object
            Dim target = ResolveType(typeName)
            Try
                Return target.InvokeMember(methodName,
                    BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Static Or
                    BindingFlags.InvokeMethod Or BindingFlags.OptionalParamBinding,
                    Nothing, Nothing, args, CultureInfo.InvariantCulture)
            Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
                Throw
            End Try
        End Function
    End Class

    ''' <summary>队列任务的实时视图；写入直接作用于宿主原对象。</summary>
    Friend NotInheritable Class HostTask
        Friend ReadOnly Property Instance As Object

        Private Sub New(value As Object)
            Instance = value
        End Sub

        Friend Shared Function Wrap(value As Object) As HostTask
            If value Is Nothing Then Return Nothing
            Return New HostTask(value)
        End Function

        Friend ReadOnly Property ID As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "ID")
            End Get
        End Property

        Friend ReadOnly Property 任务名称 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "任务名称")
            End Get
        End Property

        Friend ReadOnly Property 输入文件 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "输入文件")
            End Get
        End Property

        Friend Property 输出文件 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "输出文件")
            End Get
            Set(value As String)
                HostRuntime.WriteMember(Instance, "输出文件", value)
            End Set
        End Property

        Friend ReadOnly Property 命令行 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "命令行")
            End Get
        End Property

        Friend ReadOnly Property 正在执行 As Boolean
            Get
                Return HostRuntime.ReadValue(Of Boolean)(Instance, "正在执行")
            End Get
        End Property

        Friend ReadOnly Property IsPaused As Boolean
            Get
                ' 按枚举名称识别状态，不把宿主枚举数值复制进插件。
                Return String.Equals(Convert.ToString(HostRuntime.ReadMember(Instance, "状态"),
                    CultureInfo.InvariantCulture), "已暂停", StringComparison.Ordinal)
            End Get
        End Property

        Friend ReadOnly Property 当前进程ID As Integer
            Get
                Return HostRuntime.ReadValue(Of Integer)(Instance, "当前进程ID")
            End Get
        End Property

        Friend Property 手动停止 As Boolean
            Get
                Return HostRuntime.ReadValue(Of Boolean)(Instance, "手动停止")
            End Get
            Set(value As Boolean)
                HostRuntime.WriteMember(Instance, "手动停止", value)
            End Set
        End Property

        Friend ReadOnly Property 进度 As HostTaskProgress
            Get
                Dim value = HostRuntime.ReadMember(Instance, "进度")
                Return If(value Is Nothing, Nothing, New HostTaskProgress(value))
            End Get
        End Property
    End Class

    ''' <summary>同时支持宿主进度对象的属性和字段，不复制业务状态。</summary>
    Friend NotInheritable Class HostTaskProgress
        Private ReadOnly Instance As Object

        Friend Sub New(value As Object)
            Instance = value
        End Sub

        Friend ReadOnly Property 当前时间 As TimeSpan
            Get
                Return HostRuntime.ReadValue(Of TimeSpan)(Instance, "当前时间")
            End Get
        End Property

        Friend ReadOnly Property 总时长 As TimeSpan
            Get
                Return HostRuntime.ReadValue(Of TimeSpan)(Instance, "总时长")
            End Get
        End Property

        Friend Property 百分比 As Double
            Get
                Return HostRuntime.ReadValue(Of Double)(Instance, "百分比")
            End Get
            Set(value As Double)
                HostRuntime.WriteMember(Instance, "百分比", value)
            End Set
        End Property

        Friend Property 进度文本 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "进度文本")
            End Get
            Set(value As String)
                HostRuntime.WriteMember(Instance, "进度文本", value)
            End Set
        End Property

        Friend Property 效率文本 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "效率文本")
            End Get
            Set(value As String)
                HostRuntime.WriteMember(Instance, "效率文本", value)
            End Set
        End Property

        Friend Property 时间文本 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "时间文本")
            End Get
            Set(value As String)
                HostRuntime.WriteMember(Instance, "时间文本", value)
            End Set
        End Property

        Friend Property 当前阶段 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "当前阶段")
            End Get
            Set(value As String)
                HostRuntime.WriteMember(Instance, "当前阶段", value)
            End Set
        End Property

        Friend Property 输出大小KB As Long
            Get
                Return HostRuntime.ReadValue(Of Long)(Instance, "输出大小KB")
            End Get
            Set(value As Long)
                HostRuntime.WriteMember(Instance, "输出大小KB", value)
            End Set
        End Property

        Friend Property 输出大小文本 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "输出大小文本")
            End Get
            Set(value As String)
                HostRuntime.WriteMember(Instance, "输出大小文本", value)
            End Set
        End Property
    End Class

    Friend NotInheritable Class HostPreset
        Friend ReadOnly Property Instance As Object

        Friend Sub New(value As Object)
            Instance = value
        End Sub

        Friend ReadOnly Property 输出容器 As String
            Get
                Return HostRuntime.ReadValue(Of String)(Instance, "输出容器")
            End Get
        End Property
    End Class

    Friend NotInheritable Class HostPresetAccess
        Friend Shared ReadOnly Property 输入占位符 As String
            Get
                Return HostRuntime.ReadValue(Of String)(HostRuntime.ResolveType("预设管理_v6"), "输入占位符")
            End Get
        End Property

        Friend Shared ReadOnly Property 输出占位符 As String
            Get
                Return HostRuntime.ReadValue(Of String)(HostRuntime.ResolveType("预设管理_v6"), "输出占位符")
            End Get
        End Property

        Friend Shared ReadOnly Property 媒体总时长占位符 As String
            Get
                Return HostRuntime.ReadValue(Of String)(HostRuntime.ResolveType("预设管理_v6"), "媒体总时长占位符")
            End Get
        End Property

        Friend Shared Function 从面板创建预设(panel As Object) As HostPreset
            Return New HostPreset(HostRuntime.InvokeShared("预设管理_v6", "从面板创建预设", panel))
        End Function

        Friend Shared Function 将预设数据转换为命令行(preset As HostPreset, input As String, output As String) As String
            Return CStr(HostRuntime.InvokeShared("预设管理_v6", "将预设数据转换为命令行", preset.Instance, input, output))
        End Function

        Friend Shared Sub 刷新参数总览(panel As Object)
            HostRuntime.InvokeShared("预设管理_v6", "刷新参数总览", panel)
        End Sub
    End Class

    Friend NotInheritable Class HostSettings
        Friend Shared ReadOnly Property Instance As Object
            Get
                Dim settingsType = HostRuntime.ResolveType("设置_v6", False)
                Return If(settingsType Is Nothing, Nothing, HostRuntime.ReadMember(settingsType, "实例对象"))
            End Get
        End Property

        Friend Shared Property AlternativeProcessPath As String
            Get
                Dim settings = Instance
                Return If(settings Is Nothing, "", HostRuntime.ReadValue(Of String)(settings, "替代进程文件名"))
            End Get
            Set(value As String)
                Dim settings = Instance
                If settings IsNot Nothing Then HostRuntime.WriteMember(settings, "替代进程文件名", value)
            End Set
        End Property

        Friend Shared Function GetWorkingDirectory() As String
            If HostRuntime.ResolveType("设置_v6", False) Is Nothing Then Return ""
            Return CStr(HostRuntime.InvokeShared("设置_v6", "获取有效工作目录"))
        End Function
    End Class
End Namespace

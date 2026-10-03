Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Linq
Imports System.Reflection

Namespace videoenhancer

    ''' <summary>
    ''' 3FUI 编码队列访问层，只在运行时解析宿主类型。
    ''' 队列通过 IEnumerable 转换为本地任务视图，不绑定宿主返回类型的二进制签名。
    ''' </summary>
    Friend NotInheritable Class HostQueueAccess

        Private Shared ReadOnly Property QueueType As Type
            Get
                Return HostRuntime.ResolveType("编码队列_v6", False)
            End Get
        End Property
        Private Shared ReadOnly PublicStatic As BindingFlags =
            BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Static

        Private Sub New()
        End Sub

        ''' <summary>优先调用 6.2.20 的获取队列快照()，旧宿主回退到队列属性。</summary>
        Public Shared Function GetQueueSnapshot() As List(Of HostTask)
            If QueueType Is Nothing Then Return New List(Of HostTask)()
            Dim reflected = InvokeSharedNoArg("获取队列快照")
            Dim result = ConvertTasks(reflected)
            If result.Count > 0 OrElse reflected IsNot Nothing Then
                Return result
            End If

            ' 旧宿主仍导出队列属性；通过反射读取，避免在插件元数据中绑定旧 getter 签名。
            Try
                Dim propertyInfo = QueueType.GetProperty("队列", PublicStatic)
                If propertyInfo IsNot Nothing Then
                    Return ConvertTasks(propertyInfo.GetValue(Nothing))
                End If
            Catch ex As Exception
                LogFailure("读取宿主队列属性失败", ex)
            End Try
            Return New List(Of HostTask)()
        End Function

        ''' <summary>优先调用宿主按 ID 索引；方法不存在时在快照中查找。</summary>
        Public Shared Function FindTask(id As String) As HostTask
            If String.IsNullOrWhiteSpace(id) OrElse QueueType Is Nothing Then
                Return Nothing
            End If
            Try
                Dim method = QueueType.GetMethod("根据ID获取任务", PublicStatic, Nothing, New Type() {GetType(String)}, Nothing)
                If method IsNot Nothing Then
                    Return HostTask.Wrap(method.Invoke(Nothing, New Object() {id}))
                End If
            Catch ex As Exception
                LogFailure("按 ID 读取宿主队列任务失败", ex)
            End Try
            Return GetQueueSnapshot().FirstOrDefault(Function(task) task IsNot Nothing AndAlso
                String.Equals(task.ID, id, StringComparison.Ordinal))
        End Function

        ''' <summary>
        ''' 调用宿主队列控制 API（暂停、恢复、停止、移除、重置等）。
        ''' 参数统一为 String ID 数组，匹配 6.2.20 的 IEnumerable(Of String) 签名。
        ''' </summary>
        Public Shared Function InvokeTaskCommand(commandName As String, ids As IEnumerable(Of String)) As Boolean
            If String.IsNullOrWhiteSpace(commandName) OrElse ids Is Nothing OrElse QueueType Is Nothing Then
                Return False
            End If
            Try
                Dim method = QueueType.GetMethods(PublicStatic).FirstOrDefault(
                    Function(candidate)
                        If Not String.Equals(candidate.Name, commandName, StringComparison.Ordinal) Then Return False
                        Dim parameters = candidate.GetParameters()
                        Return parameters.Length = 1 AndAlso
                            GetType(IEnumerable(Of String)).IsAssignableFrom(parameters(0).ParameterType)
                    End Function)
                If method Is Nothing Then
                    Return False
                End If
                method.Invoke(Nothing, New Object() {ids.ToArray()})
                Return True
            Catch ex As Exception
                LogFailure("调用宿主任务命令失败：" & commandName, ex)
                Return False
            End Try
        End Function

        ''' <summary>尝试调用单个任务实例的公开/友元方法（如停止、暂停、恢复）。</summary>
        Public Shared Function InvokeTaskInstance(task As HostTask, methodName As String) As Boolean
            If task Is Nothing OrElse String.IsNullOrWhiteSpace(methodName) Then Return False
            Try
                Dim method = task.Instance.GetType().GetMethod(methodName,
                    BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance,
                    Nothing, Type.EmptyTypes, Nothing)
                If method Is Nothing Then Return False
                method.Invoke(task.Instance, Nothing)
                Return True
            Catch ex As Exception
                LogFailure("调用任务实例方法失败：" & methodName, ex)
                Return False
            End Try
        End Function

        Private Shared Function InvokeSharedNoArg(methodName As String) As Object
            Try
                Dim method = QueueType.GetMethod(methodName, PublicStatic, Nothing, Type.EmptyTypes, Nothing)
                If method Is Nothing Then Return Nothing
                Return method.Invoke(Nothing, Nothing)
            Catch ex As Exception
                LogFailure("调用宿主队列快照方法失败：" & methodName, ex)
                Return Nothing
            End Try
        End Function

        Private Shared Function ConvertTasks(value As Object) As List(Of HostTask)
            Dim result As New List(Of HostTask)()
            Dim enumerable = TryCast(value, IEnumerable)
            If enumerable Is Nothing Then Return result
            Try
                For Each item In enumerable
                    Dim task = HostTask.Wrap(item)
                    If task IsNot Nothing Then result.Add(task)
                Next
            Catch ex As Exception
                LogFailure("转换宿主队列快照失败", ex)
            End Try
            Return result
        End Function

        Private Shared Sub LogFailure(message As String, ex As Exception)
            Try
                Trace.WriteLine("[VideoEnhancer][队列兼容] " & message & "：" & ex.Message)
            Catch
            End Try
        End Sub

        Friend Shared Function ComputeOutputPath(input As String, preset As HostPreset,
                                                autoRename As Boolean, reserved As HashSet(Of String)) As String
            Return CStr(HostRuntime.InvokeShared("编码队列_v6", "计算输出位置_v6",
                input, preset.Instance, autoRename, reserved))
        End Function

    End Class

End Namespace

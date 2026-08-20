Imports System
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Windows.Forms
Imports FFmpegFreeUI

Namespace videoenhancer

    ''' <summary>访问 3fui 宿主内部对象（VB 默认实例 / 私有字段），并提供测试注入点。</summary>
    Friend Class HostAccess

        Private Shared ReadOnly TestOverrides As New Dictionary(Of String, Object)(StringComparer.Ordinal)

        ''' <summary>供测试注入指定类型的实例（优先于 My.Forms 默认实例）。</summary>
        Public Shared Sub SetTestOverride(formClassName As String, instance As Object)
            TestOverrides(formClassName) = instance
        End Sub

        ''' <summary>获取 3fui 中 VB 默认实例（My.Forms）对应的窗体。</summary>
        Public Shared Function GetDefaultInstance(formClassName As String) As Object
            If TestOverrides.ContainsKey(formClassName) Then
                Return TestOverrides(formClassName)
            End If

            Dim asm = GetType(FormMain_v6).Assembly
            Dim instance = ResolveMyFormsInstance(asm, formClassName)
            If instance IsNot Nothing Then
                Return instance
            End If

            Try
                For Each openForm As Form In Application.OpenForms
                    If openForm.GetType().Name = formClassName Then
                        Return openForm
                    End If
                Next
            Catch
            End Try
            Return Nothing
        End Function

        ''' <summary>
        ''' 通过宿主的 My.MyProject.Forms 获取默认窗体实例（兼容 Friend 成员、不同根命名空间与
        ''' 静态/实例 Forms 属性）。VB 的 My.Forms 首次访问会惰性创建窗体，因此无需窗体已打开。
        ''' </summary>
        Private Shared Function ResolveMyFormsInstance(asm As Assembly, formClassName As String) As Object
            Try
                Dim myProject As Type = Nothing
                Dim asmName = asm.GetName().Name
                For Each candidate In {asmName & ".My.MyProject", "FFmpegFreeUI.My.MyProject", "My.MyProject"}
                    Dim t = asm.GetType(candidate, False)
                    If t IsNot Nothing Then
                        myProject = t
                        Exit For
                    End If
                Next
                If myProject Is Nothing Then
                    For Each t In asm.GetTypes()
                        If t.Name = "MyProject" AndAlso t.Namespace IsNot Nothing AndAlso
                           t.Namespace.EndsWith("My", StringComparison.OrdinalIgnoreCase) Then
                            myProject = t
                            Exit For
                        End If
                    Next
                End If
                If myProject Is Nothing Then
                    Return Nothing
                End If

                Dim flags = BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Static Or BindingFlags.Instance
                Dim formsProp = myProject.GetProperty("Forms", flags)
                If formsProp Is Nothing Then
                    Return Nothing
                End If

                Dim forms As Object = Nothing
                Dim getter = formsProp.GetGetMethod(True)
                If getter IsNot Nothing AndAlso getter.IsStatic Then
                    forms = formsProp.GetValue(Nothing)
                Else
                    Dim target As Object = Nothing
                    Dim currentProp = myProject.GetProperty("Current", flags)
                    If currentProp IsNot Nothing Then
                        target = currentProp.GetValue(Nothing)
                    End If
                    If target Is Nothing Then
                        Dim currentField = myProject.GetField("Current", flags)
                        If currentField IsNot Nothing Then
                            target = currentField.GetValue(Nothing)
                        End If
                    End If
                    If target Is Nothing Then
                        Try
                            target = Activator.CreateInstance(myProject, True)
                        Catch
                        End Try
                    End If
                    If target IsNot Nothing Then
                        forms = formsProp.GetValue(target)
                    End If
                End If

                If forms Is Nothing Then
                    Return Nothing
                End If
                Dim formProp = forms.GetType().GetProperty(formClassName, BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance)
                If formProp Is Nothing Then
                    Return Nothing
                End If
                Return formProp.GetValue(forms)
            Catch
                Return Nothing
            End Try
        End Function

        Public Shared Function GetField(target As Object, ParamArray fieldNames As String()) As Object
            If target Is Nothing Then
                Return Nothing
            End If
            Dim t = target.GetType()
            For Each name As String In fieldNames
                Dim f = t.GetField(name, BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance)
                If f IsNot Nothing Then
                    Return f.GetValue(target)
                End If
            Next
            Dim baseType = t.BaseType
            While baseType IsNot Nothing
                For Each name As String In fieldNames
                    Dim f = baseType.GetField(name, BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance)
                    If f IsNot Nothing Then
                        Return f.GetValue(target)
                    End If
                Next
                baseType = baseType.BaseType
            End While
            Return Nothing
        End Function

        Public Shared Function GetProperty(target As Object, name As String) As Object
            If target Is Nothing Then
                Return Nothing
            End If
            Dim p = target.GetType().GetProperty(name, BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance)
            If p Is Nothing Then
                Return Nothing
            End If
            Return p.GetValue(target)
        End Function

        Public Shared Sub SetProperty(target As Object, name As String, value As Object)
            If target Is Nothing Then
                Return
            End If
            Dim p = target.GetType().GetProperty(name, BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Instance)
            If p IsNot Nothing Then
                p.SetValue(target, value)
            End If
        End Sub

        ''' <summary>寻找"加入编码队列"按钮（兼容编译后带下划线前缀的字段名）。</summary>
        Public Shared Function FindQueueButton(prepareForm As Object) As Control
            Return TryCast(GetField(prepareForm, "_MB_加入编码队列", "MB_加入编码队列"), Control)
        End Function

        ''' <summary>获取准备文件列表（UltraDetailListView）。</summary>
        Public Shared Function GetFileListView(prepareForm As Object) As Object
            Return GetField(prepareForm, "_UltraDetailListView1", "UltraDetailListView1")
        End Function

    End Class

End Namespace

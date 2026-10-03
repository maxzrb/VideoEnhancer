Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Reflection
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports LakeUI

Namespace videoenhancer

    Public Partial Class PluginPanel

        ' ── 模型转换器页 ──
        Private ReadOnly _lblConvertInput As New HtmlColorLabel()
        Private ReadOnly _lblConvertOutput As New HtmlColorLabel()
        Private ReadOnly _lblConvertStatus As New HtmlColorLabel()
        Private ReadOnly _btnPickPth As New ModernButton()
        Private ReadOnly _btnConvert As New ModernButton()
        Private _convertInputPath As String = ""
        Private _convertIsInterpolation As Boolean = False
        Private _convertArchitecture As String = ""
        Private _conversionRunning As Boolean = False
        ' ────────────────────────── 模型转换器页 ──────────────────────────

        Private Sub BuildOfficialConverterPage()
            _pageConverter.Dock = DockStyle.Fill
            _pageConverter.BackColor = Color.Transparent
            _pageConverter.Padding = New Padding(0, 4, 0, 0)
            _pageConverter.AllowDrop = True
            AddHandler _pageConverter.DragEnter, AddressOf OnConverterDragEnter
            AddHandler _pageConverter.DragDrop, AddressOf OnConverterDragDrop

            Dim root As New ModernGridPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, CSng(UiRowHeight)))
            AddHandler root.DragEnter, AddressOf OnConverterDragEnter
            AddHandler root.DragDrop, AddressOf OnConverterDragDrop
            root.AddAt(CreateOfficialSectionHeading(
                "模型转换", "超分权重与 RIFE 补帧权重使用各自的 TensorRT 构建流程"), 0, 0)

            Dim inputRow As New ModernGridPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty,
                .AllowDrop = True
            }
            inputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140.0F))
            inputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, CSng(UiColumnGap)))
            inputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            inputRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _btnPickPth.Text = "选择或拖入权重"
            _btnPickPth.Dock = DockStyle.Fill
            _btnPickPth.Margin = New Padding(0, 4, 0, 4)
            ConfigureSecondaryButton(_btnPickPth)
            AddHandler _btnPickPth.Click, AddressOf OnPickPthClick
            _lblConvertInput.Text = "<font color=#888888>支持 .pth / .pt / .pkl</font>"
            _lblConvertInput.AutoSize = False
            _lblConvertInput.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            inputRow.AddAt(_btnPickPth, 0, 0)
            inputRow.AddAt(CreateOfficialValueBox(_lblConvertInput), 2, 0)
            AddHandler inputRow.DragEnter, AddressOf OnConverterDragEnter
            AddHandler inputRow.DragDrop, AddressOf OnConverterDragDrop
            root.AddAt(inputRow, 0, 1)

            Dim outputRow As New ModernGridPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 3,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            outputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140.0F))
            outputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, CSng(UiColumnGap)))
            outputRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            outputRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            Dim outputCaption = CreateOfficialCaption("输出目录")
            outputCaption.TextAlign = ContentAlignment.MiddleLeft
            outputCaption.Padding = New Padding(8, 0, 0, 0)
            _lblConvertOutput.Text = "<font color=#888888>选择模型后自动确定</font>"
            _lblConvertOutput.AutoSize = False
            _lblConvertOutput.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            outputRow.AddAt(outputCaption, 0, 0)
            outputRow.AddAt(CreateOfficialValueBox(_lblConvertOutput), 2, 0)
            root.AddAt(outputRow, 0, 2)

            Dim information As New HtmlColorLabel With {
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0, 12, 0, 8),
                .Padding = Padding.Empty,
                .BackColor1 = Color.Transparent,
                .BorderSize = 0,
                .ForeColor = UiTextMuted,
                .AutoSize = False,
                .LineSpacing = 7,
                .TextAlign = HtmlColorLabel.TextAlignEnum.TopLeft,
                .Text = "<font color=#DCDCDC><b>PyTorch 权重 → TensorRT Engine</b></font><br/>" &
                        "<font color=#888888>超分 .pth 归档到 TensorRT-Personalized；RIFE 按权重结构识别并构建 flow/encode 缓存。</font><br/>" &
                        "<font color=#888888>转换完全在本机进行，不会上传模型；复杂模型可能需要数分钟。</font><br/>" &
                        "<font color=#888888>Engine 与显卡、TensorRT 和 CUDA 版本绑定，换设备后建议重新转换。</font>"
            }
            root.AddAt(information, 0, 3)

            Dim actionRow As New ModernGridPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .BackColor = Color.Transparent,
                .Margin = Padding.Empty,
                .Padding = Padding.Empty
            }
            actionRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 148.0F))
            actionRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            actionRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _btnConvert.Text = "开始离线转换"
            _btnConvert.Dock = DockStyle.Fill
            _btnConvert.Margin = New Padding(0, 4, 0, 4)
            _btnConvert.Enabled = False
            ConfigurePrimaryButton(_btnConvert)
            AddHandler _btnConvert.Click, AddressOf OnConvertModelClick
            _lblConvertStatus.Text = "<font color=#888888>等待选择模型…</font>"
            _lblConvertStatus.AutoSize = False
            _lblConvertStatus.Dock = DockStyle.Fill
            _lblConvertStatus.Margin = New Padding(16, 0, 0, 0)
            _lblConvertStatus.TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft
            actionRow.AddAt(_btnConvert, 0, 0)
            actionRow.AddAt(_lblConvertStatus, 1, 0)
            root.AddAt(actionRow, 0, 4)
            _pageConverter.Controls.Add(root)
        End Sub

        Private Sub OnConverterDragEnter(sender As Object, e As DragEventArgs)
            If e.Data IsNot Nothing AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim paths = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
                If paths IsNot Nothing AndAlso paths.Length > 0 AndAlso
                    IsPyTorchWeightExtension(paths(0)) Then
                    e.Effect = DragDropEffects.Copy
                    Return
                End If
            End If
            e.Effect = DragDropEffects.None
        End Sub

        Private Sub OnConverterDragDrop(sender As Object, e As DragEventArgs)
            Dim paths = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
            If paths IsNot Nothing AndAlso paths.Length > 0 Then
                SelectConverterInput(paths(0))
            End If
        End Sub

        Private Sub OnPickPthClick(sender As Object, e As EventArgs)
            Using dialog As New OpenFileDialog With {
                .Title = "选择超分或补帧 PyTorch 权重",
                .Filter = "PyTorch 权重 (*.pth;*.pt;*.pkl)|*.pth;*.pt;*.pkl|所有文件 (*.*)|*.*",
                .CheckFileExists = True,
                .Multiselect = False
            }
                If dialog.ShowDialog(Me) = DialogResult.OK Then
                    SelectConverterInput(dialog.FileName)
                End If
            End Using
        End Sub

        Private Async Sub SelectConverterInput(modelPath As String)
            If Not File.Exists(modelPath) OrElse Not IsPyTorchWeightExtension(modelPath) Then
                SetConverterStatus("只支持有效的 .pth、.pt 或 .pkl 权重文件。", True)
                Return
            End If
            _convertInputPath = Path.GetFullPath(modelPath)
            _convertIsInterpolation = False
            _convertArchitecture = ""
            _btnConvert.Enabled = False
            SetConverterStatus("正在读取权重结构并识别模型架构…", False)

            Dim inspection = Await Task.Run(Function() InspectConverterModel(_convertInputPath))
            If Not String.Equals(_convertInputPath, Path.GetFullPath(modelPath), StringComparison.OrdinalIgnoreCase) Then Return
            If inspection.Item1 Then
                _convertIsInterpolation = True
                _convertArchitecture = inspection.Item2
            ElseIf Not String.IsNullOrWhiteSpace(inspection.Item3) AndAlso
                Not String.Equals(Path.GetExtension(_convertInputPath), ".pth", StringComparison.OrdinalIgnoreCase) Then
                SetConverterStatus(inspection.Item3, True)
                Return
            End If

            Dim outputDir = If(_convertIsInterpolation,
                               Path.GetDirectoryName(_convertInputPath),
                               GetPersonalizedTensorRtDirectory())
            _lblConvertInput.Text = "<font color=#DCDCDC>" & EscapeHtml(_convertInputPath) & "</font>"
            _lblConvertOutput.Text = "<font color=#DCDCDC>" & EscapeHtml(outputDir) & "</font>"
            _btnConvert.Enabled = Not _conversionRunning
            If _convertIsInterpolation Then
                _btnConvert.Text = "预构建 1080p RIFE Engine"
                SetConverterStatus("已识别 " & _convertArchitecture & "；将使用 RVE flow/encode 专用构建流程。", False)
            Else
                _btnConvert.Text = "开始转换  →"
                SetConverterStatus("超分模型已就绪，点击「开始转换」。", False)
            End If
        End Sub

        Private Shared Function IsPyTorchWeightExtension(filePath As String) As Boolean
            Dim extension = Path.GetExtension(filePath)
            Return String.Equals(extension, ".pth", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(extension, ".pt", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(extension, ".pkl", StringComparison.OrdinalIgnoreCase)
        End Function

        Private Async Sub OnConvertModelClick(sender As Object, e As EventArgs)
            If _conversionRunning OrElse Not File.Exists(_convertInputPath) Then Return
            Dim coreRoot = ResolveCoreRoot()
            Dim pythonExe = Path.Combine(coreRoot, "python", "python", "python.exe")
            Dim converter = Path.Combine(coreRoot, "python", "backend", "convert_tensorrt.py")
            Dim outputDir = GetPersonalizedTensorRtDirectory()
            Dim rifePrepare = Path.Combine(coreRoot, "python", "backend", "prepare_rife_tensorrt.py")
            If Not File.Exists(pythonExe) OrElse
               (Not _convertIsInterpolation AndAlso Not File.Exists(converter)) OrElse
               (_convertIsInterpolation AndAlso Not File.Exists(rifePrepare)) Then
                SetConverterStatus("找不到便携 Python 或所需的 TensorRT 构建脚本，请检查 videoenhancer.exe 同目录下的 python。", True)
                Return
            End If

            If Not _convertIsInterpolation Then Directory.CreateDirectory(outputDir)
            _conversionRunning = True
            _btnConvert.Enabled = False
            _btnPickPth.Enabled = False
            SetConverterStatus("正在离线编译 TensorRT Engine；复杂模型可能需要数分钟，请勿关闭程序…", False)
            Try
                Dim progress = Sub(line As String)
                                   Dim match = Regex.Match(line.Trim(), "^VIDEOENHANCER_TRT_PROGRESS\|[^|]+\|(\d+)\|?(.*)$", RegexOptions.IgnoreCase)
                                   If Not match.Success Then Return
                                   Dim percent As Integer
                                   If Not Integer.TryParse(match.Groups(1).Value, percent) Then Return
                                   percent = Math.Max(0, Math.Min(100, percent))
                                   Dim detail = match.Groups(2).Value.Trim()
                                   Dim statusText = "构建 TensorRT Engine " & percent.ToString() & "%" & If(String.IsNullOrWhiteSpace(detail), "", "：" & detail)
                                   Try
                                       If Not IsDisposed AndAlso IsHandleCreated Then
                                           BeginInvoke(New Action(Sub() SetConverterStatus(statusText, False)))
                                       End If
                                   Catch ex As InvalidOperationException
                                   End Try
                               End Sub
                Dim result = Await Task.Run(
                    Function()
                        If _convertIsInterpolation Then
                            Return RunRifeTensorRtPrepare(pythonExe, rifePrepare, _convertInputPath, 1920, 1080, progress)
                        End If
                        Return RunTensorRtConversion(pythonExe, converter, _convertInputPath, outputDir, progress)
                    End Function)
                If result.Item1 = 0 Then
                    Dim enginePath = LastEnginePath(result.Item2)
                    If _convertIsInterpolation Then
                        SetConverterStatus("RIFE Engine 已就绪；实际任务分辨率不同时会自动构建对应缓存。", False)
                        If _config.InterpBackend = "tensorrt" Then RefreshInterpModels()
                    Else
                        SetConverterStatus("转换完成：" & If(String.IsNullOrWhiteSpace(enginePath), outputDir, enginePath), False)
                        If _config.Backend = "tensorrt" Then RefreshUpscaleModels()
                    End If
                Else
                    SetConverterStatus("转换失败：" & LastNonEmptyLine(result.Item2), True)
                End If
            Catch ex As Exception
                SetConverterStatus("转换失败：" & ex.Message, True)
            Finally
                _conversionRunning = False
                _btnPickPth.Enabled = True
                _btnConvert.Enabled = File.Exists(_convertInputPath)
            End Try
        End Sub

        Private Shared Function RunTensorRtConversion(pythonExe As String, converter As String, inputPath As String, outputDir As String, progress As Action(Of String)) As Tuple(Of Integer, String)
            Dim psi As New ProcessStartInfo With {
                .FileName = pythonExe,
                .WorkingDirectory = Path.GetDirectoryName(converter),
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8,
                .StandardErrorEncoding = Encoding.UTF8
            }
            PortableRuntime.ConfigureProcess(psi)
            psi.ArgumentList.Add(converter)
            psi.ArgumentList.Add(inputPath)
            psi.ArgumentList.Add("--output-dir")
            psi.ArgumentList.Add(outputDir)
            Using child As Diagnostics.Process = Diagnostics.Process.Start(psi)
                If child Is Nothing Then Return New Tuple(Of Integer, String)(1, "无法启动模型转换进程")
                Dim output As New StringBuilder()
                Dim outputHandler As DataReceivedEventHandler = Sub(sender, e)
                                                                      If String.IsNullOrWhiteSpace(e.Data) Then Return
                                                                      SyncLock output
                                                                          output.AppendLine(e.Data)
                                                                      End SyncLock
                                                                      progress?.Invoke(e.Data)
                                                                  End Sub
                Dim errorHandler As DataReceivedEventHandler = Sub(sender, e)
                                                                     If String.IsNullOrWhiteSpace(e.Data) Then Return
                                                                     SyncLock output
                                                                         output.AppendLine(e.Data)
                                                                     End SyncLock
                                                                 End Sub
                AddHandler child.OutputDataReceived, outputHandler
                AddHandler child.ErrorDataReceived, errorHandler
                child.BeginOutputReadLine()
                child.BeginErrorReadLine()
                child.WaitForExit()
                ' 第二次等待确保异步事件已把尾部输出写入结果。
                child.WaitForExit()
                RemoveHandler child.OutputDataReceived, outputHandler
                RemoveHandler child.ErrorDataReceived, errorHandler
                Return New Tuple(Of Integer, String)(child.ExitCode, output.ToString())
            End Using
        End Function

        Private Function ResolveCoreRoot() As String
            Return PluginConfig.ApplicationRoot
        End Function

        Private Function GetPersonalizedTensorRtDirectory() As String
            Return Path.Combine(ResolveCoreRoot(), "models", "TensorRT-Personalized")
        End Function

        Private Sub SetConverterStatus(text As String, isError As Boolean)
            Dim color = If(isError, "#F4707A", "#53D2A2")
            _lblConvertStatus.Text = "<font color=" & color & ">" & EscapeHtml(If(text, "")) & "</font>"
        End Sub

        Friend Shared Function LastNonEmptyLine(text As String) As String
            If String.IsNullOrWhiteSpace(text) Then Return "未返回详细信息"
            Dim lines = text.Replace(Convert.ToChar(13), Convert.ToChar(10)).Split(Convert.ToChar(10))
            For i As Integer = lines.Length - 1 To 0 Step -1
                If Not String.IsNullOrWhiteSpace(lines(i)) Then Return lines(i).Trim()
            Next
            Return "未返回详细信息"
        End Function

        Private Shared Function LastEnginePath(text As String) As String
            If Not String.IsNullOrWhiteSpace(text) Then
                Dim lines = text.Replace(Convert.ToChar(13), Convert.ToChar(10)).Split(Convert.ToChar(10))
                For i As Integer = lines.Length - 1 To 0 Step -1
                    Dim line = lines(i).Trim()
                    If line.EndsWith(".engine", StringComparison.OrdinalIgnoreCase) AndAlso Not line.Contains("|"c) Then
                        Return line
                    End If
                Next
            End If
            Return LastNonEmptyLine(text)
        End Function

        ''' <summary>从 CLI 标准错误中提取可直接展示给用户的错误正文。</summary>
        Private Shared Function CliErrorMessage(text As String, fallback As String) As String
            If String.IsNullOrWhiteSpace(text) Then Return fallback
            Dim lines = text.Replace(Convert.ToChar(13), Convert.ToChar(10)).Split(Convert.ToChar(10))
            For Each rawLine In lines
                Dim line = rawLine.Trim()
                If line.StartsWith("[错误]", StringComparison.Ordinal) Then
                    Return line.Substring(4).Trim()
                End If
            Next
            For Each rawLine In lines
                Dim line = rawLine.Trim()
                If line.Length > 0 AndAlso Not line.Contains("|") Then Return line
            Next
            Return fallback
        End Function

        Private Function InspectConverterModel(modelPath As String) As Tuple(Of Boolean, String, String)
            Dim coreRoot = ResolveCoreRoot()
            Dim pythonExe = Path.Combine(coreRoot, "python", "python", "python.exe")
            Dim inspector = Path.Combine(coreRoot, "python", "backend", "inspect_interpolation_models.py")
            If Not File.Exists(pythonExe) OrElse Not File.Exists(inspector) Then
                Return New Tuple(Of Boolean, String, String)(False, "", "补帧模型架构检查器未安装")
            End If
            Dim capture = RunProcessCaptureUtf8(pythonExe, Path.GetDirectoryName(inspector),
                                                New String() {inspector, modelPath})
            Dim jsonLine = capture.Item2.Replace(Convert.ToChar(13).ToString(), "").
                Split(Convert.ToChar(10)).
                Select(Function(line) line.Trim()).LastOrDefault(Function(line) line.StartsWith("["))
            If String.IsNullOrWhiteSpace(jsonLine) Then
                Return New Tuple(Of Boolean, String, String)(False, "", LastNonEmptyLine(capture.Item2))
            End If
            Try
                Using document = JsonDocument.Parse(jsonLine)
                    Dim item = document.RootElement(0)
                    Dim architecture = item.GetProperty("architecture").GetString()
                    Dim canTensorRt = item.GetProperty("tensorrt").GetBoolean()
                    Dim modelError = item.GetProperty("error").GetString()
                    If canTensorRt Then Return New Tuple(Of Boolean, String, String)(True, architecture, "")
                    If Not String.IsNullOrWhiteSpace(architecture) Then
                        Return New Tuple(Of Boolean, String, String)(False, architecture,
                            architecture & " 当前只支持 CUDA/PyTorch 补帧，不支持 TensorRT。")
                    End If
                    Return New Tuple(Of Boolean, String, String)(False, "", modelError)
                End Using
            Catch ex As Exception
                Return New Tuple(Of Boolean, String, String)(False, "", "模型架构检查失败：" & ex.Message)
            End Try
        End Function

        Private Shared Function RunProcessCaptureUtf8(fileName As String, workingDirectory As String,
                                                       arguments As IEnumerable(Of String)) As Tuple(Of Integer, String)
            Dim psi As New ProcessStartInfo With {
                .FileName = fileName, .WorkingDirectory = workingDirectory,
                .UseShellExecute = False, .CreateNoWindow = True,
                .RedirectStandardOutput = True, .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
            }
            PortableRuntime.ConfigureProcess(psi)
            For Each argument In arguments
                psi.ArgumentList.Add(argument)
            Next
            Using child = Diagnostics.Process.Start(psi)
                If child Is Nothing Then Return New Tuple(Of Integer, String)(1, "无法启动模型检查进程")
                Dim stdout = child.StandardOutput.ReadToEnd()
                Dim stderr = child.StandardError.ReadToEnd()
                child.WaitForExit()
                Return New Tuple(Of Integer, String)(child.ExitCode, stdout & Environment.NewLine & stderr)
            End Using
        End Function

        Private Shared Function RunRifeTensorRtPrepare(pythonExe As String, prepareScript As String,
                                                       inputPath As String, width As Integer, height As Integer,
                                                       progress As Action(Of String)) As Tuple(Of Integer, String)
            Dim psi As New ProcessStartInfo With {
                .FileName = pythonExe,
                .WorkingDirectory = Path.GetDirectoryName(prepareScript),
                .UseShellExecute = False, .CreateNoWindow = True,
                .RedirectStandardOutput = True, .RedirectStandardError = True,
                .StandardOutputEncoding = Encoding.UTF8, .StandardErrorEncoding = Encoding.UTF8
            }
            PortableRuntime.ConfigureProcess(psi)
            For Each argument In New String() {prepareScript, inputPath, "--width", width.ToString(), "--height", height.ToString()}
                psi.ArgumentList.Add(argument)
            Next
            Using child = Diagnostics.Process.Start(psi)
                If child Is Nothing Then Return New Tuple(Of Integer, String)(1, "无法启动 RIFE TensorRT 构建进程")
                Dim output As New StringBuilder()
                Dim outputHandler As DataReceivedEventHandler =
                    Sub(sender, e)
                        If String.IsNullOrWhiteSpace(e.Data) Then Return
                        SyncLock output
                            output.AppendLine(e.Data)
                        End SyncLock
                        progress?.Invoke(e.Data)
                    End Sub
                Dim errorHandler As DataReceivedEventHandler =
                    Sub(sender, e)
                        If String.IsNullOrWhiteSpace(e.Data) Then Return
                        SyncLock output
                            output.AppendLine(e.Data)
                        End SyncLock
                    End Sub
                AddHandler child.OutputDataReceived, outputHandler
                AddHandler child.ErrorDataReceived, errorHandler
                child.BeginOutputReadLine()
                child.BeginErrorReadLine()
                child.WaitForExit()
                child.WaitForExit()
                RemoveHandler child.OutputDataReceived, outputHandler
                RemoveHandler child.ErrorDataReceived, errorHandler
                Return New Tuple(Of Integer, String)(child.ExitCode, output.ToString())
            End Using
        End Function

    End Class

End Namespace

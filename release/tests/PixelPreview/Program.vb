Imports System
Imports System.Drawing
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Windows.Forms
Imports System.Threading.Tasks
Imports System.Collections
Imports System.Runtime.InteropServices
Imports Vortice.Direct2D1
Imports LakeUI
Imports videoenhancer

Module Program
    <STAThread>
    Sub Main(args As String())
        Using preview As New PixelPictureBox(), first As New Bitmap(64, 48), second As New Bitmap(128, 96)
            PixelPreviewImage.SetImage(preview, first)
            CheckSize(preview, 64, 48)
            PixelPreviewImage.SetImage(preview, second)
            CheckSize(preview, 128, 96)
            PixelPreviewImage.SetImage(preview, Nothing)
            Dim propertyInfo = GetType(PixelPictureBox).GetProperty("Image")
            If propertyInfo Is Nothing Then propertyInfo = GetType(PixelPictureBox).GetProperty("Source")
            If propertyInfo.GetValue(preview) IsNot Nothing Then Throw New Exception("Preview not cleared")
            If second.GetPixel(0, 0) <> Color.FromArgb(0) Then Throw New Exception("Caller-owned image was disposed")
        End Using
        If args.Length > 0 Then CheckPluginCallback(args(0))
        If args.Contains("--render") Then CheckRenderedFrames(args(0))
        Console.WriteLine("PIXEL_PREVIEW_PASS|" & GetType(PixelPictureBox).Assembly.GetName().Version.ToString() & "|assign|replace|clear|ownership")
    End Sub

    Private Sub CheckRenderedFrames(pluginPath As String)
        Dim panelType = Assembly.LoadFrom(pluginPath).GetType("videoenhancer.PluginPanel", throwOnError:=True)
        Dim panel = RuntimeHelpers.GetUninitializedObject(panelType)
        Dim flags = BindingFlags.Instance Or BindingFlags.NonPublic
        Using window As New Form With {.ShowInTaskbar = False, .StartPosition = FormStartPosition.Manual,
                                        .Location = New Point(40, 40), .ClientSize = New Size(256, 192)},
              preview As New PixelPictureBox With {.Dock = DockStyle.Fill},
              red As New Bitmap(128, 96), blue As New Bitmap(128, 96)
            Using drawing = Graphics.FromImage(red)
                drawing.Clear(Color.Red)
            End Using
            Using drawing = Graphics.FromImage(blue)
                drawing.Clear(Color.Blue)
            End Using
            window.Controls.Add(preview)
            panelType.GetField("_picPreview", flags).SetValue(panel, preview)
            Dim callback = panelType.GetMethod("OnPreviewFrameReady", flags)
            Dim failure As Exception = Nothing
            AddHandler window.Shown, Async Sub()
                                         Try
                                             callback.Invoke(panel, {Nothing, red})
                                             Await Task.Delay(300)
                                             Dim first = ReadCenter(preview)
                                             callback.Invoke(panel, {Nothing, blue})
                                             Await Task.Delay(300)
                                             Dim second = ReadCenter(preview)
                                             Console.WriteLine("RENDER_COLORS|" & first.ToString() & "|" & second.ToString())
                                             If first.R < 200 OrElse first.B > 60 OrElse second.B < 200 OrElse second.R > 60 Then
                                                 Throw New Exception("LakeUI同尺寸新帧未反映在实际绘制像素中")
                                             End If
                                         Catch ex As Exception
                                             failure = ex
                                         Finally
                                             PixelPreviewImage.SetImage(preview, Nothing)
                                             window.Close()
                                         End Try
                                     End Sub
            Application.Run(window)
            If failure IsNot Nothing Then Throw failure
        End Using
        Console.WriteLine("PIXEL_RENDER_PASS|same-size-red-to-blue|actual-plugin-callback")
    End Sub

    Private Function ReadCenter(preview As Control) As Color
        ' V5 直接呈现到 GPU；DrawToBitmap 不包含该表面，必须读回实际渲染位图。
        Dim flags = BindingFlags.Instance Or BindingFlags.Static Or BindingFlags.NonPublic Or BindingFlags.Public
        Dim registry = GetType(PixelPictureBox).Assembly.GetType("LakeUI.D3D_ControlSurfaceRegistry", True)
        Dim entries = DirectCast(registry.GetField("_entries", flags).GetValue(Nothing), IDictionary)
        Dim entry = entries(preview)
        If entry Is Nothing Then Throw New Exception("LakeUI未创建预览GPU表面")
        Dim surface = entry.GetType().GetField("Surface", flags).GetValue(entry)
        Dim bitmap = DirectCast(surface.GetType().GetProperty("Bitmap", flags).GetValue(surface), ID2D1Bitmap1)
        Dim context = DirectCast(surface.GetType().GetField("_context", flags).GetValue(surface), ID2D1DeviceContext)
        Dim properties As New BitmapProperties1(bitmap.PixelFormat, 96, 96, BitmapOptions.CpuRead Or BitmapOptions.CannotDraw)
        Using readback = context.CreateBitmap(bitmap.PixelSize, IntPtr.Zero, 0UI, properties)
            readback.CopyFromBitmap(bitmap).CheckError()
            Dim mapped = readback.Map(MapOptions.Read)
            Try
                If bitmap.PixelFormat.Format = Vortice.DXGI.Format.R16G16B16A16_Float Then
                    Dim offset16 = CInt((bitmap.PixelSize.Height \ 2) * mapped.Pitch + (bitmap.PixelSize.Width \ 2) * 8)
                    Dim components(3) As Integer
                    For index = 0 To 3
                        Dim value = CSng(BitConverter.Int16BitsToHalf(Marshal.ReadInt16(mapped.Bits, offset16 + index * 2)))
                        components(index) = CInt(Math.Clamp(value * 255, 0, 255))
                    Next
                    Return Color.FromArgb(components(3), components(0), components(1), components(2))
                End If
                Dim offset = CInt((bitmap.PixelSize.Height \ 2) * mapped.Pitch + (bitmap.PixelSize.Width \ 2) * 4)
                Return Color.FromArgb(Marshal.ReadInt32(mapped.Bits, offset))
            Finally
                readback.Unmap()
            End Try
        End Using
    End Function

    Private Sub CheckPluginCallback(pluginPath As String)
        Dim panelType = Assembly.LoadFrom(pluginPath).GetType("videoenhancer.PluginPanel", throwOnError:=True)
        Dim panel = RuntimeHelpers.GetUninitializedObject(panelType)
        Dim flags = BindingFlags.Instance Or BindingFlags.NonPublic
        Using preview As New PixelPictureBox(), first As New Bitmap(64, 48), second As New Bitmap(128, 96)
            panelType.GetField("_picPreview", flags).SetValue(panel, preview)
            Dim callback = panelType.GetMethod("OnPreviewFrameReady", flags)
            callback.Invoke(panel, {Nothing, first})
            CheckSize(preview, 64, 48)
            callback.Invoke(panel, {Nothing, second})
            CheckSize(preview, 128, 96)
            If Not ReferenceEquals(panelType.GetField("_lastPreviewImage", flags).GetValue(panel), second) Then Throw New Exception("Frame callback did not store the image")
            PixelPreviewImage.SetImage(preview, Nothing)
        End Using
        Console.WriteLine("PLUGIN_CALLBACK_PASS|OnPreviewFrameReady|replace")
    End Sub

    Private Sub CheckSize(preview As PixelPictureBox, width As Integer, height As Integer)
        Dim propertyInfo = GetType(PixelPictureBox).GetProperty("Image")
        If propertyInfo IsNot Nothing Then
            Dim image = DirectCast(propertyInfo.GetValue(preview), Image)
            If image.Width <> width OrElse image.Height <> height Then Throw New Exception("Image size mismatch")
        Else
            Dim source = GetType(PixelPictureBox).GetProperty("Source").GetValue(preview)
            If CLng(source.GetType().GetProperty("Width").GetValue(source)) <> width OrElse
               CLng(source.GetType().GetProperty("Height").GetValue(source)) <> height Then Throw New Exception("Source size mismatch")
        End If
    End Sub
End Module

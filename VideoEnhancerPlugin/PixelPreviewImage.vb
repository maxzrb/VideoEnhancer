Imports System.Drawing
Imports LakeUI

Namespace videoenhancer
    ''' <summary>通过公开图像源呈现预览，图像生命周期仍由预览页面管理。</summary>
    Friend NotInheritable Class PixelPreviewImage
        Friend Shared Sub SetImage(control As PixelPictureBox, image As Image)
            If image Is Nothing Then
                control.Source = Nothing
                Return
            End If

            control.Source = New PixelPictureCallbackSource(
                image.Width, image.Height,
                Function(context, destination, source, frameIndex, interpolation)
                    context.DrawImage(image, destination, source.ToRectangleF(), 1.0F, frameIndex, interpolation)
                    Return True
                End Function,
                colorSpace:=PixelPictureColorSpace.SRgb,
                transferFunction:=PixelPictureTransferFunction.SRgb,
                pixelFormat:=PixelPicturePixelFormat.Bgra32)
        End Sub
    End Class
End Namespace

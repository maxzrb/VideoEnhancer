Imports System
Imports System.Collections.Generic
Imports System.Linq

Namespace videoenhancer
        Friend NotInheritable Class ModelCatalogItem
            Public Property Id As String = ""
            Public Property DisplayName As String = ""
            Public Property ArchitectureGroup As String = ""
            Public Property InferenceScales As Integer() = Array.Empty(Of Integer)()
            Public Property Architecture As String = ""
            Public Property Purpose As String = ""
            Public Property Scale As Integer
            Public Property InputMultiple As Integer = 1
            Public Property Source As String = ""
            Public Property Backends As String() = Array.Empty(Of String)()
        End Class

        Friend NotInheritable Class UserModelItem
            Public Property Id As String = ""
            Public Property DisplayName As String = ""
            Public Property RelativePath As String = ""
            Public Property Task As String = ""
            Public Property Architecture As String = ""
            Public Property Purpose As String = ""
            Public Property Format As String = ""
            Public Property Scale As Integer
            Public Property InputMultiple As Integer = 1
            Public Property MinimumSize As Integer
            Public Property Square As Boolean
            Public Property Tiling As String = ""
            Public Property Sha256 As String = ""
            Public Property Size As Long
            Public Property ImportedAtUtc As String = ""
            Public Property Backends As String() = Array.Empty(Of String)()
        End Class
End Namespace

' This is from some sample code I wrote a long time ago to make a .NET data provider around DAO (basically MS Access). 
' You can use the OleDB provider in .NET for this, so it was really just a learning experiment.

Imports System.Collections                      ' For access to ArrayList
Imports System.Data                             ' For access to ConnectionState and StateChangeEventArgs
Imports System.Data.Common                      ' For access to DbConnection, DbCommand and DbTransaction
Imports System.Runtime.InteropServices.Marshal  ' For access to ReleaseComObject 

Public Class JetConnection 

    ' TODO: Add more constructors for all classes, support transactions, implement JetEngineVersion 

    Inherits DbConnection 

    Implements ICloneable 

    ' Since our base class inherits from System.ComponentModel.Component, the VS 2005 IDE
    ' wants to use the Designer with this class. We're not really a designable object, so
    ' that doesn't work. You have to right click on the class and select View Code. 

#Region " Private Member Variables " 

    Private m_sConnectionString As String 

    Private m_eConnectionState As ConnectionState 

    Private m_oAccess As Object     ' An Access.Application object used with late binding 

    Private m_oEngine As DAO.DBEngine 

    Private m_oWorkspace As DAO.Workspace 

    Private m_oDatabase As DAO.Database 

    Private m_oOpenReaders As ArrayList 

#End Region 

#Region " Constructors and Dispose() " 

    Public Sub New() 

        ' We could make this a Friend constructor so only JetFactory can create it, 
        ' but there's no reason to not make it public. 

        m_sConnectionString = String.Empty 

        m_eConnectionState = ConnectionState.Closed 

        m_oEngine = Nothing 

        m_oWorkspace = Nothing 

        m_oDatabase = Nothing 

        InitOpenReaderList() 

    End Sub 

    Private Sub New(ByVal oSourceConnection As JetConnection) 

        ' Used by our Clone() method below. 

        Me.New() 

        m_sConnectionString = oSourceConnection.ConnectionString 

    End Sub 

    Protected Overrides Sub Dispose(ByVal bDisposing As Boolean) 

        ' bDisposing: True to release both managed and unmanaged resources; False to release only unmanaged resources. 

        Me.Close() 

        MyBase.Dispose(bDisposing) 

    End Sub 

#End Region 

#Region " Core Public Properties " 

    Public Overrides Property ConnectionString() As String 

        Get 

            Return m_sConnectionString 

        End Get 

        Set(ByVal sConnectionString As String) 

            AssertConnectionIsClosed() 

            If sConnectionString Is Nothing Then 

                sConnectionString = String.Empty 

            End If 

            m_sConnectionString = sConnectionString 

        End Set 

    End Property 

#End Region 

#Region " Opening and Closing the Connection " 

#Region " Public Open and Close methods " 

    Public Overrides Sub Open() 

        AssertConnectionIsClosed() 

        OpenInternal() 

        ChangeState(ConnectionState.Open) 

    End Sub 

    Public Overrides Sub Close() 

        On Error Resume Next 

        If m_eConnectionState = ConnectionState.Open Then 

            CloseOpenReaders() 

            CloseInternal() 

            ChangeState(ConnectionState.Closed) 

        End If 

    End Sub 

#End Region 

#Region " Internal Open and Close Methods " 

    Private Sub OpenInternal() 

        Dim oBuilder As New JetConnectionStringBuilder(Me.ConnectionString) 

        With oBuilder 

            Select Case .Type 

                Case JetConnectionType.Direct 

                    OpenConnectionDirect(.DatabaseFile, .WorkgroupFile, .UserName, .Password, .Exclusive, .ReadOnly) 

                Case JetConnectionType.Access 

                    OpenConnectionAccess(.DatabaseFile) 

                Case JetConnectionType.Custom 

                    OpenConnectionCustom() 

                Case Else 

                    Throw New System.ArgumentOutOfRangeException("Type", My.Resources.InvalidConnectionType) 

            End Select 

        End With 

    End Sub 

    Private Sub CloseInternal() 

        Dim oBuilder As New JetConnectionStringBuilder(Me.ConnectionString) 

        With oBuilder 

            Select Case .Type 

                Case JetConnectionType.Direct 

                    CloseConnectionDirect() 

                Case JetConnectionType.Access 

                    CloseConnectionAccess() 

                Case JetConnectionType.Custom 

                    CloseConnectionCustom() 

                Case Else 

                    Throw New System.ArgumentOutOfRangeException("Type", My.Resources.InvalidConnectionType) 

            End Select 

        End With 

    End Sub 

#End Region 

#Region " Open and Close methods for direct DAO connections " 

    Private Sub OpenConnectionDirect(ByVal sDatabaseFile As String, ByVal sWorkgroupFile As String, ByVal sUserName As String, ByVal sPassword As String, ByVal bExclusive As Boolean, ByVal bReadOnly As Boolean) 

        Try 

            m_oEngine = New DAO.DBEngine 

            m_oEngine.SystemDB = sWorkgroupFile 

            m_oWorkspace = m_oEngine.CreateWorkspace("xSystem.Data.Jet", sUserName, sPassword, DAO.WorkspaceTypeEnum.dbUseJet) 

            m_oDatabase = m_oWorkspace.OpenDatabase(sDatabaseFile, bExclusive, bReadOnly)  ' Note DAO docs say the second parameter is for Exclusive 

        Catch 

            CloseConnectionDirect() 

            Throw 

        End Try 

    End Sub 

    Private Sub CloseConnectionDirect() 

        On Error Resume Next 

        If m_oDatabase IsNot Nothing Then 

            m_oDatabase.Close() 

            ReleaseComObject(m_oDatabase) : m_oDatabase = Nothing 

        End If 

        If m_oWorkspace IsNot Nothing Then 

            m_oWorkspace.Close() 

            ReleaseComObject(m_oWorkspace) : m_oWorkspace = Nothing 

        End If 

        If m_oEngine IsNot Nothing Then 

            m_oEngine.Idle(DAO.IdleEnum.dbFreeLocks) 

            ReleaseComObject(m_oEngine) : m_oEngine = Nothing 

        End If 

    End Sub 

#End Region 

#Region " Open and Close methods for indirect Access connections " 

    Private Sub OpenConnectionAccess(ByVal sDatabaseFile As String) 

        Try 

            m_oAccess = GetComServerByFile(sDatabaseFile) 

            m_oEngine = DirectCast(LateBinding.GetProperty(m_oAccess, "DBEngine"), DAO.DBEngine) 

            Using oWorkspaces As New ComObject(Of DAO.Workspaces)(m_oEngine.Workspaces) 

                m_oWorkspace = oWorkspaces.Target.Item(0) 

            End Using 

            m_oDatabase = DirectCast(LateBinding.InvokeFunction(m_oAccess, "CurrentDb"), DAO.Database) 

        Catch 

            CloseConnectionAccess() 

            Throw 

        End Try 

    End Sub 

    Private Sub CloseConnectionAccess() 

        On Error Resume Next 

        If m_oDatabase IsNot Nothing Then 

            ReleaseComObject(m_oDatabase) : m_oDatabase = Nothing 

        End If 

        If m_oWorkspace IsNot Nothing Then 

            ReleaseComObject(m_oWorkspace) : m_oWorkspace = Nothing 

        End If 

        If m_oEngine IsNot Nothing Then 

            ReleaseComObject(m_oEngine) : m_oEngine = Nothing 

        End If 

        If m_oAccess IsNot Nothing Then 

            ReleaseComObject(m_oAccess) : m_oAccess = Nothing 

        End If 

    End Sub 

    Private Function GetComServerByFile(ByVal sDatabaseFile As String) As Object 

        Try 

            Return Microsoft.VisualBasic.GetObject(PathName:=sDatabaseFile) 

        Catch oExecption As System.Exception 

            Throw New System.IO.FileNotFoundException(My.Resources.CannotGetComServerByFile, sDatabaseFile, oExecption) 

        End Try 

    End Function 

#End Region 

#Region " Open and Close methods for custom connections " 

    Public Event OpenConnection(ByVal oSender As Object, ByVal oArguments As JetConnectionOpenEventArgs)
    Public Event CloseConnection(ByVal oSender As Object, ByVal oArguments As JetConnectionCloseEventArgs) 

    Private Sub OpenConnectionCustom() 

        Try 

            Dim oArguments As New JetConnectionOpenEventArgs(m_oEngine, m_oWorkspace, m_oDatabase) 

            RaiseEvent OpenConnection(Me, oArguments) 

            ValidateOpenEventArgs(oArguments) 

            m_oEngine = DirectCast(oArguments.Engine, DAO.DBEngine)
            m_oWorkspace = DirectCast(oArguments.Workspace, DAO.Workspace)
            m_oDatabase = DirectCast(oArguments.Database, DAO.Database) 

        Catch 

            CloseConnectionCustom() 

            Throw 

        End Try 

    End Sub 

    Private Sub CloseConnectionCustom() 

        On Error Resume Next 

        RaiseEvent CloseConnection(Me, New JetConnectionCloseEventArgs(m_oEngine, m_oWorkspace, m_oDatabase)) 

        m_oDatabase = Nothing
        m_oWorkspace = Nothing
        m_oEngine = Nothing 

    End Sub 

#End Region 

#End Region 

#Region " Information Properties " 

    Public Overrides ReadOnly Property State() As System.Data.ConnectionState 

        Get 

            Return m_eConnectionState 

        End Get 

    End Property 

    Public Overrides ReadOnly Property Database() As String 

        ' Gets the name of the current database or the database to be used after a connection is opened.
        ' The default value is an empty string. 

        Get 

            If Me.State = ConnectionState.Open Then 

                Return m_oDatabase.Name 

            Else 

                Dim oBuilder As New JetConnectionStringBuilder(Me.ConnectionString) 

                Return oBuilder.DatabaseFile 

            End If 

        End Get 

    End Property 

    Public Overrides ReadOnly Property DataSource() As String 

        ' This is suppossed to represent the server, which Jet doesn't have since it's not server based.
        ' We return what type of connection they are looking for, a direct connection, or one throug Access. 

        Get 

            Return String.Empty 

        End Get 

    End Property 

    Public Overrides ReadOnly Property ServerVersion() As String 

        ' Gets a string that contains the version of the "server" to which the client is connected.  
        ' If no connection is open, an empty string is returned. 

        Get 

            Return Me.JetEngineVersion 

        End Get 

    End Property 

    Public ReadOnly Property DaoEngineVersion() As String 

        ' Gets a string that contains the version of DAO _currently in use_, if the connection is open.  
        ' If no connection is open, an empty string is returned. 

        Get 

            If Me.State = ConnectionState.Open Then 

                Return m_oEngine.Version 

            Else 

                Return String.Empty 

            End If 

        End Get 

    End Property 

    Public ReadOnly Property JetEngineVersion() As String 

        ' Gets a string that contains the version of Jet _currently in use_, if the connection is open.  
        ' If no connection is open, an empty string is returned. 

        Get 

            If Me.State = ConnectionState.Open Then 

                Return String.Empty ' TODO: Implement this. 

            Else 

                Return String.Empty 

            End If 

        End Get 

    End Property 

    Public ReadOnly Property JetDatabaseVersion() As String 

        ' Gets a string that contains the version of Jet _used to create the database_, if the connection is open.  
        ' If no connection is open, an empty string is returned. 

        Get 

            If Me.State = ConnectionState.Open Then 

                Return m_oDatabase.Version 

            Else 

                Return String.Empty 

            End If 

        End Get 

    End Property 

    Public Overridable ReadOnly Property AccessEngineVersion() As String 

        ' Gets a string that contains the version of Access _currently in use_, if the connection is open.  
        ' If no connection is open, an empty string is returned. 

        Get 

            If (Me.State = ConnectionState.Open) AndAlso (m_oAccess IsNot Nothing) Then 

                Return CStr(LateBinding.InvokeFunction(m_oAccess, "Syscmd", 7)) ' 7 = acSysCmdAccessVer 

            Else 

                Return String.Empty 

            End If 

        End Get 

    End Property 

    Public Overridable ReadOnly Property AccessDatabaseVersion() As String 

        ' Gets a string that contains the version of Access _used to create the database_, if the connection is open.  
        ' If no connection is open, an empty string is returned. 

        Get 

            If Me.State = ConnectionState.Open Then 

                Try 

                    Using oDaoProperties As New ComObject(Of DAO.Properties)(m_oDatabase.Properties) 

                        Using oDaoProperty As New ComObject(Of DAO.Property)(oDaoProperties.Target.Item("AccessVersion")) 

                            Return CStr(oDaoProperty.Target.Value) 

                        End Using 

                    End Using 

                Catch oException As Exception 

                    Return String.Empty 

                End Try 

            Else 

                Return String.Empty 

            End If 

        End Get 

    End Property 

#End Region 

#Region " Event-Related Methods " 

    Private Sub ChangeState(ByVal eNewState As ConnectionState) 

        Dim eOldState As ConnectionState 

        eOldState = m_eConnectionState 

        m_eConnectionState = eNewState 

        If eOldState <> eNewState Then 

            Me.OnStateChange(New StateChangeEventArgs(eOldState, eNewState)) 

        End If 

    End Sub 

#End Region 

#Region " Other methods we are required to override " 

    Protected Overrides Function CreateDbCommand() As DbCommand 

        Return New JetCommand(Me) 

    End Function 

    Public Overloads Function CreateCommand() As JetCommand 

        ' We call MyBase's CreateCommand, which will call back down into our CreateDbCommand above. 

        Return DirectCast(MyBase.CreateCommand(), JetCommand) 

    End Function 

#End Region 

#Region " Collection of Open DataReaders " 

    Private Sub InitOpenReaderList() 

        m_oOpenReaders = ArrayList.Repeat(Nothing, 5) 

    End Sub 

    Private Sub CloseOpenReaders() 

        Dim oReader As JetDataReader 

        For Each oReader In m_oOpenReaders 

            If oReader IsNot Nothing Then 

                oReader.Close() 

            End If 

        Next 

        InitOpenReaderList() 

    End Sub 

    Friend Sub AttachReader(ByVal oReader As JetDataReader) 

        Dim iIndex As Integer 

        iIndex = m_oOpenReaders.IndexOf(Nothing) 

        If iIndex <> -1 Then 

            m_oOpenReaders.Item(iIndex) = oReader 

        Else 

            m_oOpenReaders.Add(oReader) 

        End If 

    End Sub 

    Friend Sub DetachReader(ByVal oReader As JetDataReader) 

        Dim iIndex As Integer 

        iIndex = m_oOpenReaders.IndexOf(oReader) 

        If iIndex <> -1 Then 

            m_oOpenReaders.Item(iIndex) = Nothing 

        End If 

    End Sub 

#End Region 

#Region " Unsupported Methods " 

    Public Overrides Sub ChangeDatabase(ByVal databaseName As String) 

        ' Changes the current database for an open connection. 

        Throw New System.NotSupportedException 

    End Sub 

    Protected Overrides Function BeginDbTransaction(ByVal isolationLevel As System.Data.IsolationLevel) As System.Data.Common.DbTransaction 

        Throw New System.NotSupportedException 

    End Function 

#End Region 

#Region " ICloneable Implementation " 

    Public Function Clone() As Object Implements ICloneable.Clone 

        Return New JetConnection(Me) 

    End Function 

#End Region 

#Region " Friend Methods Used by Other Objects " 

    Friend ReadOnly Property DaoDatabase() As DAO.Database 

        Get 

            Return m_oDatabase 

        End Get 

    End Property 

#End Region 

#Region " Private Validation and Assertion Routines " 

    Private Sub AssertConnectionIsOpen() 

        If Me.State <> ConnectionState.Open Then 

            Throw New System.InvalidOperationException(My.Resources.ConnectionNotOpen) 

        End If 

    End Sub 

    Private Sub AssertConnectionIsClosed() 

        If Me.State <> ConnectionState.Closed Then 

            Throw New System.InvalidOperationException(My.Resources.ConnectionNotClosed) 

        End If 

    End Sub 

    Public Sub ValidateOpenEventArgs(ByVal oArguments As JetConnectionOpenEventArgs) 

        If (oArguments.Engine Is Nothing) Then 

            Throw New System.ArgumentNullException("Engine") 

        End If 

        If Not GetType(DAO.DBEngine).IsInstanceOfType(oArguments.Engine) Then 

            Throw New System.ArgumentException(My.Resources.InvalidObjectType, "Engine") 

        End If 

        If (oArguments.Workspace Is Nothing) Then 

            Throw New System.ArgumentNullException("Workspace") 

        End If 

        If Not GetType(DAO.Workspace).IsInstanceOfType(oArguments.Workspace) Then 

            Throw New System.ArgumentException(My.Resources.InvalidObjectType, "Workspace") 

        End If 

        If (oArguments.Database Is Nothing) Then 

            Throw New System.ArgumentNullException("Database") 

        End If 

        If Not GetType(DAO.Database).IsInstanceOfType(oArguments.Database) Then 

            Throw New System.ArgumentException(My.Resources.InvalidObjectType, "Database") 

        End If 

    End Sub 

#End Region 

End Class



 
Imports System.Data                             ' For access to CommandType, CommandBehavior, UpdateRowSource
Imports System.Data.Common                      ' For access to DbConnection, DbCommand, DbParameter, DbParameterCollection
Imports System.Runtime.InteropServices.Marshal  ' For access to ReleaseComObject 

Public Class JetCommand 

    ' TODO: Nothing 

    Inherits DbCommand 

    Implements ICloneable 

    ' The JetCommand object is just a "definition" of a command. In our implementation, it's not actually
    ' involved in reading data. The JetDataReader doesn't need to know what JetCommand created it.
    ' You can change the JetCommand object at any time without affecting JetDataReaders that it created. 

    ' Since our base class inherits from System.ComponentModel.Component, the VS 2005 IDE
    ' wants to use the Designer with this class. We're not really a designable object, so
    ' that doesn't work. You have to right click on the class and select View Code. 

#Region " Private Member Variables " 

    Private m_eType As CommandType 

    Private m_sText As String 

    Private m_iTimeout As Integer 

    Private m_oParameters As JetParameterCollection 

    Private m_oConnection As JetConnection 

    Private m_eUpdatedRowSource As UpdateRowSource      ' We don't really use this property ourselves. 

    Private m_bDesignTimeVisible As Boolean             ' We don't really use this property ourselves. 

#End Region 

#Region " Constructors and Dispose() " 

    Public Sub New() 

        ' We could make this a Friend constructor so only JetFactory can create it, 
        ' but there's no reason to not make it public. 

        m_eType = CommandType.Text 

        m_sText = String.Empty 

        m_iTimeout = 30 

        m_oConnection = Nothing 

        m_oParameters = New JetParameterCollection 

        m_eUpdatedRowSource = UpdateRowSource.Both 

        m_bDesignTimeVisible = False 

    End Sub 

    Friend Sub New(ByVal oConnection As JetConnection) 

        ' Used by JetConnection.CreateDbCommand() 

        Me.New() 

        m_oConnection = oConnection 

    End Sub 

    Private Sub New(ByVal oSourceCommand As JetCommand) 

        ' Constructor used by our Clone() method below. 

        With oSourceCommand 

            m_eType = .CommandType 

            m_sText = .CommandText 

            m_iTimeout = .CommandTimeout 

            m_oConnection = .Connection 

            m_oParameters = DirectCast(.Parameters.Clone(), JetParameterCollection) 

            m_eUpdatedRowSource = .UpdatedRowSource 

            m_bDesignTimeVisible = .DesignTimeVisible 

        End With 

    End Sub 

    Protected Overrides Sub Dispose(ByVal bDisposing As Boolean) 

        ' bDisposing: True to release both managed and unmanaged resources; False to release only unmanaged resources. 

        m_eType = CommandType.Text 

        m_sText = String.Empty 

        m_oConnection = Nothing 

        m_oParameters.Clear() 

        m_oParameters = Nothing 

        MyBase.Dispose(bDisposing) 

    End Sub 

#End Region 

#Region " Core Command Properties" 

    Protected Overrides Property DbConnection() As DbConnection 

        ' We're required to override this method. It's used by our base class. 

        Get 

            Return m_oConnection 

        End Get 

        Set(ByVal oConnection As DbConnection) 

            ValidateConnectionType(oConnection) 

            m_oConnection = DirectCast(oConnection, JetConnection) 

        End Set 

    End Property 

    Public Overloads Property Connection() As JetConnection 

        ' This allows the Connection property to get/set a JetConnection type rather than just a DbConnection type.
        ' We use MyBase's Connection property, which will then call down into our DbConnection property above. 

        Get 

            Return DirectCast(MyBase.Connection, JetConnection) 

        End Get 

        Set(ByVal oConnection As JetConnection) 

            MyBase.Connection = oConnection 

        End Set 

    End Property 

    Public Overrides Property CommandType() As CommandType 

        Get 

            Return m_eType 

        End Get 

        Set(ByVal eType As CommandType) 

            Select Case eType 

                Case CommandType.Text, CommandType.StoredProcedure, CommandType.TableDirect 

                    m_eType = eType 

                Case Else 

                    Throw New System.ArgumentOutOfRangeException("CommandType", My.Resources.InvalidCommandType) 

            End Select 

        End Set 

    End Property 

    Public Overrides Property CommandText() As String 

        Get 

            If m_sText Is Nothing Then 

                Return String.Empty 

            Else 

                Return m_sText 

            End If 

        End Get 

        Set(ByVal sText As String) 

            If sText Is Nothing Then 

                m_sText = String.Empty 

            Else 

                m_sText = sText 

            End If 

        End Set 

    End Property 

    Public Overrides Property CommandTimeout() As Integer 

        Get 

            Return m_iTimeout 

        End Get 

        Set(ByVal iTimeout As Integer) 

            If m_iTimeout < 0 Then 

                Throw New System.ArgumentException(My.Resources.InvalidCommandTimeout, "CommandTimeout") 

            End If 

            m_iTimeout = iTimeout 

        End Set 

    End Property 

#End Region 

#Region " Parameters " 

    Protected Overrides Function CreateDbParameter() As DbParameter 

        Return New JetParameter 

    End Function 

    Public Overloads Function CreateParameter() As JetParameter 

        ' This gives us a CreateParameter method that returns a JetParameter rather than just a DbParameter.
        ' We call MyBase's CreateParameter, which will call back down into our CreateDbParameter above. 

        Return DirectCast(MyBase.CreateParameter, JetParameter) 

    End Function 

    Protected Overrides ReadOnly Property DbParameterCollection() As DbParameterCollection 

        Get 

            Return m_oParameters 

        End Get 

    End Property 

    Public Overloads ReadOnly Property Parameters() As JetParameterCollection 

        Get 

            ' This gives us a Parameters property that returns a JetParameterCollection rather than just a DbParameterCollection.
            ' We call MyBase's Parameters, which will call back down into our DbParameterCollection above. 

            Return DirectCast(MyBase.Parameters, JetParameterCollection) 

        End Get 

    End Property 

#End Region 

#Region " Execute Methods " 

#Region " ExecuteScalar() " 

    Public Overrides Function ExecuteScalar() As Object 

        Using oReader As JetDataReader = Me.ExecuteReader() 

            If (oReader.Read) AndAlso (oReader.FieldCount >= 1) Then 

                Return oReader.GetValue(0) 

            Else 

                Return Nothing 

            End If 

        End Using 

    End Function 

#End Region 

#Region " ExecuteNonQuery() " 

    Public Overrides Function ExecuteNonQuery() As Integer 

        AssertCommandTextIsNotEmpty() 

        AssertConnectionIsOpen() 

        Select Case Me.CommandType 

            Case CommandType.TableDirect 

                Throw New System.ArgumentOutOfRangeException(My.Resources.WrongCommandType) 

            Case CommandType.Text 

                Return ExecuteNonQueryForSqlText() 

            Case CommandType.StoredProcedure 

                Return ExecuteNonQueryForStoredProcedure() 

        End Select 

    End Function 

    Private Function ExecuteNonQueryForSqlText() As Integer 

        ' For temporary querys, DAO will automatically create the Parameters collection with the correct parameters
        ' based on the SQL text we are using. All we have to do now is set the values. 

        Using oDaoQueryDef As New ComObject(Of DAO.QueryDef)(Me.Connection.DaoDatabase.CreateQueryDef("", Me.CommandText)) 

            AssignParameterValuesToDaoQueryDef(oDaoQueryDef.Target) 

            oDaoQueryDef.Target.Execute(0) 

            Return oDaoQueryDef.Target.RecordsAffected 

        End Using 

    End Function 

    Private Function ExecuteNonQueryForStoredProcedure() As Integer 

        Using oDaoQueryDef As New ComObject(Of DAO.QueryDef)(GetDaoQueryDefByName(Me.CommandText)) 

            AssignParameterValuesToDaoQueryDef(oDaoQueryDef.Target) 

            oDaoQueryDef.Target.Execute(0) 

            Return oDaoQueryDef.Target.RecordsAffected 

        End Using 

    End Function 

#End Region 

#Region " ExecuteReader() " 

#Region " Core DataReader Routines " 

    Protected Overrides Function ExecuteDbDataReader(ByVal eBehavior As CommandBehavior) As DbDataReader 

        AssertCommandTextIsNotEmpty() 

        AssertConnectionIsOpen() 

        Return OpenReader(eBehavior) 

    End Function 

    Private Function OpenReader(ByVal eBehavior As CommandBehavior) As JetDataReader 

        Dim oDaoRecordset As DAO.Recordset = Nothing 

        Try 

            Select Case Me.CommandType 

                Case CommandType.TableDirect 

                    oDaoRecordset = OpenRecordsetForTable() 

                Case CommandType.Text 

                    oDaoRecordset = OpenRecordsetForSqlText() 

                Case CommandType.StoredProcedure 

                    oDaoRecordset = OpenRecordsetForStoredProcedure() 

            End Select 

            Return New JetDataReader(oDaoRecordset, Me.Connection, eBehavior) 

        Catch 

            If oDaoRecordset IsNot Nothing Then 

                ReleaseComObject(oDaoRecordset) : oDaoRecordset = Nothing 

            End If 

            Throw 

        End Try 

    End Function 

#End Region 

#Region " OpenRecordset Routines " 

    Private Function OpenRecordsetForTable() As DAO.Recordset 

        ' You canno use DAO.RecordsetTypeEnum.dbOpenForwardOnly here. 

        Return Me.Connection.DaoDatabase.OpenRecordset(Me.CommandText, DAO.RecordsetTypeEnum.dbOpenTable, 0, DAO.RecordsetOptionEnum.dbReadOnly) 

    End Function 

    Private Function OpenRecordsetForSqlText() As DAO.Recordset 

        ' For temporary querys, DAO will automatically create the Parameters collection with the correct parameters
        ' based on the SQL text we are using. All we have to do now is set the values. 

        Using oDaoQueryDef As New ComObject(Of DAO.QueryDef)(Me.Connection.DaoDatabase.CreateQueryDef("", Me.CommandText)) 

            AssignParameterValuesToDaoQueryDef(oDaoQueryDef.Target) 

            OpenRecordsetForSqlText = oDaoQueryDef.Target.OpenRecordset(DAO.RecordsetTypeEnum.dbOpenForwardOnly, 0, DAO.RecordsetOptionEnum.dbReadOnly) 

            MsgBox(OpenRecordsetForSqlText.BOF & " " & OpenRecordsetForSqlText.EOF) 

            'Return 

        End Using 

    End Function 

    Private Function OpenRecordsetForStoredProcedure() As DAO.Recordset 

        Using oDaoQueryDef As New ComObject(Of DAO.QueryDef)(GetDaoQueryDefByName(Me.CommandText)) 

            AssignParameterValuesToDaoQueryDef(oDaoQueryDef.Target) 

            Return oDaoQueryDef.Target.OpenRecordset(DAO.RecordsetTypeEnum.dbOpenForwardOnly, 0, DAO.RecordsetOptionEnum.dbReadOnly) 

        End Using 

    End Function 

#End Region 

#Region " Overloaded ExecuteReader methods " 

    Public Overloads Function ExecuteReader() As JetDataReader 

        ' This gives us an ExecuteReader method that returns a JetDataReader rather than just a DbDataReader.
        ' We call MyBase's ExecuteReader, which will call back down into our ExecuteDbDataReader below. 

        Return DirectCast(MyBase.ExecuteReader(), JetDataReader) 

    End Function 

    Public Overloads Function ExecuteReader(ByVal eBehavior As CommandBehavior) As JetDataReader 

        ' This gives us an ExecuteReader method that returns a JetDataReader rather than just a DbDataReader.
        ' We call MyBase's ExecuteReader, which will call back down into our ExecuteDbDataReader below. 

        Return DirectCast(MyBase.ExecuteReader(eBehavior), JetDataReader) 

    End Function 

#End Region 

#End Region 

#Region " Parameter Routines " 

    Private Sub AssignParameterValuesToDaoQueryDef(ByVal oDaoQueryDef As DAO.QueryDef) 

        Using oDaoParameters As New ComObject(Of DAO.Parameters)(oDaoQueryDef.Parameters) 

            AssignParameterValuesToDaoParameters(oDaoParameters.Target) 

        End Using 

    End Sub 

    Private Sub AssignParameterValuesToDaoParameters(ByVal oDaoParameters As DAO.Parameters) 

        ' Here we want to assign the values from our parameters to the DAO parameter objects. This is tough since
        ' the order of our parameters may not match the order of the DAO parameters. First we want to find all of 
        ' our named parameters and match them up. Then for all of our unnamed parameters (or ones whose name we
        ' did not find), we match them up to the remaining DAO parameters in the order they appear in our collection. 

        Dim yJetParamsUnassigned As JetParameter()
        Dim yJetParamsInDaoOrder As JetParameter() 

        Dim iDaoIndex As Integer, iJetIndex As Integer, iMaxIndex As Integer 

        Dim oJetParameter As JetParameter 

        Dim sDaoParameterName As String 

        If oDaoParameters.Count <> Me.Parameters.Count Then 

            Throw New System.InvalidOperationException(My.Resources.ParameterCountMismatch) 

        End If 

        iMaxIndex = Me.Parameters.Count - 1 

        yJetParamsUnassigned = New JetParameter(iMaxIndex) {}
        yJetParamsInDaoOrder = New JetParameter(iMaxIndex) {} 

        ' All of the parameters start out unassigned. Eventually they will all be moved out of this array.
        ' The assigned array will start out with all its entries set to Nothing, indicated no match yet. 

        Me.Parameters.CopyTo(yJetParamsUnassigned, 0) 

        ' We go through all of the DAO parameters and look for matching named parameters. If we find a matching
        ' named parameter, we remove it from the unassigned list, and put it in the assigned list at the same
        ' index position of the matching DAO parameter. 

        For iDaoIndex = 0 To iMaxIndex 

            Using oDaoParameter As New ComObject(Of DAO.Parameter)(oDaoParameters.Item(iDaoIndex)) 

                sDaoParameterName = oDaoParameter.Target.Name 

                For iJetIndex = 0 To iMaxIndex 

                    oJetParameter = yJetParamsUnassigned(iJetIndex) 

                    If oJetParameter IsNot Nothing Then 

                        If String.Compare(sDaoParameterName, oJetParameter.ParameterName, StringComparison.CurrentCultureIgnoreCase) = 0 Then 

                            yJetParamsInDaoOrder(iDaoIndex) = oJetParameter
                            yJetParamsUnassigned(iJetIndex) = Nothing 

                            Exit For 

                        End If 

                    End If 

                Next 

            End Using 

        Next 

        ' Now we go through the unassigned parameters and we look for slots in the assigned parameters array
        ' that haven't gotten parameters assigned to them yet. We assign them to those unused slots in the 
        ' order they appear in our collection. 

        For iJetIndex = 0 To iMaxIndex 

            oJetParameter = yJetParamsUnassigned(iJetIndex) 

            If oJetParameter IsNot Nothing Then 

                For iDaoIndex = 0 To iMaxIndex 

                    If yJetParamsInDaoOrder(iDaoIndex) Is Nothing Then 

                        yJetParamsInDaoOrder(iDaoIndex) = oJetParameter
                        yJetParamsUnassigned(iJetIndex) = Nothing 

                    End If 

                Next 

            End If 

        Next 

        ' Now we know how all of our parameters match up with the DAO parameters. yJetParamsInDaoOrder holds
        ' references to our JetParameters in the same order as their matching parameters in the DAO parameter collection. 

        For iDaoIndex = 0 To iMaxIndex 

            Using oDaoParameter As New ComObject(Of DAO.Parameter)(oDaoParameters.Item(iDaoIndex)) 

                oDaoParameter.Target.Value = yJetParamsInDaoOrder(iDaoIndex).CoercedValue 

            End Using 

        Next 

    End Sub 

#End Region 

#End Region 

#Region " Methods and properties we don't use but must override " 

    Public Overrides Sub Cancel() 

        ' We silently ignore this method. 

    End Sub 

    Public Overrides Sub Prepare() 

        ' We silently ignore this method. 

    End Sub 

    Public Overrides Property UpdatedRowSource() As System.Data.UpdateRowSource 

        Get 

            Return m_eUpdatedRowSource 

        End Get 

        Set(ByVal eUpdatedRowSource As System.Data.UpdateRowSource) 

            m_eUpdatedRowSource = eUpdatedRowSource 

        End Set 

    End Property 

    Protected Overrides Property DbTransaction() As System.Data.Common.DbTransaction 

        Get 

            Throw New System.NotImplementedException 

        End Get 

        Set(ByVal oTransaction As System.Data.Common.DbTransaction) 

            Throw New System.NotImplementedException 

        End Set 

    End Property 

    Public Overrides Property DesignTimeVisible() As Boolean 

        ' I'm not really sure what to do with this. 

        Get 

            Return m_bDesignTimeVisible 

        End Get 

        Set(ByVal bDesignTimeValue As Boolean) 

            m_bDesignTimeVisible = bDesignTimeValue 

        End Set 

    End Property 

#End Region 

#Region " ICloneable Implementation " 

    Public Function Clone() As Object Implements ICloneable.Clone 

        Return New JetCommand(Me) 

    End Function 

#End Region 

#Region " Friend Methods Used by Other Jet Objects " 

    Friend Sub DeriveParameters() 

        ' This is called by JetCommandBuilder.DeriveParameters(). 

        Dim iIndex As Integer, iMaxIndex As Integer 

        Dim yParameters() As JetParameter 

        AssertStoredProcIsValid() 

        AssertConnectionIsOpen() 

        Using oDaoQueryDef As New ComObject(Of DAO.QueryDef)(GetDaoQueryDefByName(Me.CommandText)) 

            Using oDaoParameters As New ComObject(Of DAO.Parameters)(oDaoQueryDef.Target.Parameters) 

                iMaxIndex = oDaoParameters.Target.Count - 1 

                yParameters = New JetParameter(iMaxIndex) {} 

                For iIndex = 0 To iMaxIndex 

                    Using oDaoParameter As New ComObject(Of DAO.Parameter)(oDaoParameters.Target.Item(iIndex)) 

                        yParameters(iIndex) = New JetParameter(oDaoParameter.Target) 

                    End Using 

                Next 

            End Using 

        End Using 

        Me.Parameters.Clear() 

        Me.Parameters.AddRange(yParameters) 

    End Sub 

#End Region 

#Region " Private Methods " 

    Private Function GetDaoQueryDefByName(ByVal sQueryName As String) As DAO.QueryDef 

        Using oDaoQueryDefs As New ComObject(Of DAO.QueryDefs)(Me.Connection.DaoDatabase.QueryDefs) 

            Try 

                Return oDaoQueryDefs.Target.Item(sQueryName) 

            Catch oException As Exception 

                Throw New System.ArgumentException(My.Resources.StoredProcedureNotFound) 

            End Try 

        End Using 

    End Function 

#End Region 

#Region " Private Validation and Assertion Routines " 

    Private Sub AssertConnectionIsOpen() 

        If (Me.Connection Is Nothing) Or (Me.Connection.State <> ConnectionState.Open) Then 

            Throw New System.InvalidOperationException(My.Resources.ConnectionNotOpen) 

        End If 

    End Sub 

    Private Sub AssertCommandTextIsNotEmpty() 

        If String.IsNullOrEmpty(Me.CommandText) Then 

            Throw New System.InvalidOperationException(My.Resources.EmptyCommandText) 

        End If 

    End Sub 

    Private Sub AssertStoredProcIsValid() 

        If Me.CommandType <> System.Data.CommandType.StoredProcedure Then 

            Throw New System.ArgumentOutOfRangeException(My.Resources.WrongCommandType) 

        End If 

        AssertCommandTextIsNotEmpty() 

    End Sub 

    Private Sub ValidateConnectionType(ByVal oConnection As DbConnection) 

        If (oConnection IsNot Nothing) AndAlso (Not GetType(JetConnection).IsInstanceOfType(oConnection)) Then 

            Throw New System.InvalidCastException(My.Resources.InvalidObjectType) 

        End If 

    End Sub 

#End Region 

End Class



 
Imports System.Data                             ' For access to CommandBehavior
Imports System.Data.Common                      ' For access to DbDataReader and DbEnumerator
Imports System.Runtime.InteropServices.Marshal  ' For access to ReleaseComObject 

Public Class JetDataReader 

    ' TODO: Implement GetSchemaTable() 

    Inherits DbDataReader 

#Region " Private member variables " 

    Private m_oDaoRecordset As DAO.Recordset 

    Private m_oDaoFields As DAO.Fields          ' We cache m_oDaoRecordset.Fields for quick access. 

    Private m_iFieldCount As Integer            ' We cache m_oDaoFields.Count for quick access. 

    Private m_oFieldCache As JetFieldArray 

    Private m_bReadCalledAtLeastOnce As Boolean 

    Private m_bRecordIsReady As Boolean 

    Private m_oConnection As JetConnection 

    Private m_bCloseConnection As Boolean 

#End Region 

#Region " Constructor and Close() " 

    Friend Sub New(ByVal oDaoRecordset As DAO.Recordset, ByVal oConnection As JetConnection, ByVal eBehavior As CommandBehavior) 

        MyBase.New() 

        If oDaoRecordset Is Nothing Then Throw New ArgumentNullException("DaoRecordset") 

        If oConnection Is Nothing Then Throw New ArgumentNullException("Connection") 

        m_oDaoRecordset = oDaoRecordset 

        m_oDaoFields = oDaoRecordset.Fields 

        m_iFieldCount = m_oDaoFields.Count 

        m_oFieldCache = New JetFieldArray(m_iFieldCount) 

        m_bReadCalledAtLeastOnce = False 

        m_bRecordIsReady = False 

        m_oConnection = oConnection 

        If (eBehavior And CommandBehavior.CloseConnection) = CommandBehavior.CloseConnection Then 

            m_bCloseConnection = True 

        Else 

            m_bCloseConnection = False 

        End If 

        m_oConnection.AttachReader(Me) 

    End Sub 

    Public Overrides Sub Close() 

        ' Note that we don't need to override Dispose(), since our base class's Dispose() call's Close(). 

        On Error Resume Next 

        m_oFieldCache = Nothing 

        If m_oDaoFields IsNot Nothing Then 

            ReleaseComObject(m_oDaoFields) : m_oDaoFields = Nothing 

        End If 

        If m_oDaoRecordset IsNot Nothing Then 

            m_oDaoRecordset.Close() 

            ReleaseComObject(m_oDaoRecordset) : m_oDaoRecordset = Nothing 

        End If 

        m_oConnection.DetachReader(Me) 

        If m_bCloseConnection Then 

            m_oConnection.Close() 

        End If 

        m_oConnection = Nothing 

    End Sub 

#End Region 

#Region " Read() method " 

    Public Overrides Function Read() As Boolean 

        ' From the .NET documentation:
        ' Advances the reader to the next record in a result set. Returns True if there are more rows; otherwise False.
        ' The default position of a data reader is before the first record. Therefore, you must call Read to begin accessing data. 

        ' Really, we return True if data can be read from us after we return, False if not. 

        ' From DAO documentation:
        ' The AbsolutePosition property isn't available on forward-only–type Recordset objects...
        ' The BOF property returns True if the current record position is before the first record, and False if the current record position is on or after the first record.
        ' The EOF property returns True if the current record position is after the last record, and False if the current record position is on or before the last record
        ' If either the BOF or EOF property is True, there is no current record.
        ' If you open a Recordset object containing no records, the BOF and EOF properties are set to True. When you open a Recordset 
        ' object that contains at least one record, the first record is the current record and the BOF and EOF properties are False
        ' If you use the MoveLast method on a Recordset object containing records, the last record becomes the current record; 
        ' if you then use the MoveNext method, the current record becomes invalid and the EOF property is set to True. 
        ' Conversely, if you use the MoveFirst method on a Recordset object containing records, the first record becomes the 
        ' current record; if you then use the MovePrevious method, there is no current record and the BOF property is set to True. 

        Dim bBOF As Boolean
        Dim bEOF As Boolean 

        AssertReaderIsOpen() 

        With m_oDaoRecordset 

            bBOF = .BOF
            bEOF = .EOF 

            If bBOF And bEOF Then 

                ' There are no records in the Recordset 

                m_bRecordIsReady = False 

            ElseIf bBOF Then 

                ' There are records in the Recordset, and we're positioned before the first one. Let's move to it.
                ' This really shouldn't happen, since DAO always starts Recordsets out positioned on the first record. 

                .MoveFirst() 

                m_oFieldCache.Load(m_oDaoFields) 

                m_bRecordIsReady = True 

            ElseIf bEOF Then 

                ' There are records in the Recordset, and we're positioned after all of them. We can't go back, so return False. 

                m_bRecordIsReady = False 

            Else 

                ' There are records in the Recordset and we're positioned on one of them right now. Since DAO starts out Recordsets
                ' positioned on the first record, but .NET DataReaders are suppossed to start out positioned _before_ the first
                ' record, we have to do some checking to see if we should move forward. If so, we then check if we've most past
                ' the end. If so, EOF will now be True, which means there are no more records, so we return False. 

                If Not m_bReadCalledAtLeastOnce Then 

                    ' Read() has never been called before. Since we start out positioned on a record, we don't need to do anything. 

                    m_bReadCalledAtLeastOnce = True 

                    m_bRecordIsReady = True 

                Else 

                    .MoveNext() 

                    If .EOF Then 

                        ' We're positioned after the last record, so no record can be read now. 

                        m_bRecordIsReady = False 

                    Else 

                        ' We're now positioned on a valid record. 

                        m_bRecordIsReady = True 

                    End If 

                End If 

                If m_bRecordIsReady Then 

                    m_oFieldCache.Load(m_oDaoFields) 

                End If 

            End If 

        End With 

        Return m_bRecordIsReady 

    End Function 

#End Region 

#Region " Information about the DataReader " 

    Public Overrides ReadOnly Property IsClosed() As Boolean 

        Get 

            If m_oDaoRecordset Is Nothing Then 

                Return True 

            Else 

                Return False 

            End If 

        End Get 

    End Property 

    Public Overrides ReadOnly Property HasRows() As Boolean 

        Get 

            ' From DAO documentation: If you open a Recordset object containing no records, then BOF and EOF are set to True. 

            AssertReaderIsOpen() 

            With m_oDaoRecordset 

                If .BOF And .EOF Then 

                    Return False 

                Else 

                    Return True 

                End If 

            End With 

        End Get 

    End Property 

    Public Overrides ReadOnly Property FieldCount() As Integer 

        Get 

            AssertReaderIsOpen() 

            Return m_iFieldCount 

        End Get 

    End Property 

#End Region 

#Region " Information about the current column " 

    Public Overrides Function GetName(ByVal iOrdinal As Integer) As String 

        Return GetField(iOrdinal).GetName() 

    End Function 

    Public Overrides Function GetFieldType(ByVal iOrdinal As Integer) As System.Type 

        Return GetField(iOrdinal).GetFieldType() 

    End Function 

    Public Overrides Function GetDataTypeName(ByVal iOrdinal As Integer) As String 

        Return GetField(iOrdinal).GetDataTypeName() 

    End Function 

    Public Overrides Function IsDBNull(ByVal iOrdinal As Integer) As Boolean 

        Return GetField(iOrdinal).IsDBNull 

    End Function 

#End Region 

#Region " Get methods based on the column name " 

    Default Public Overloads Overrides ReadOnly Property Item(ByVal sColumnName As String) As Object 

        Get 

            Return Me.GetValue(Me.GetOrdinal(sColumnName)) 

        End Get 

    End Property 

    Public Overrides Function GetOrdinal(ByVal sColumnName As String) As Integer 

        Dim iIndex As Integer 

        AssertReaderIsOpen() 

        AssertRecordIsReady() 

        For iIndex = 0 To m_iFieldCount - 1 

            If String.Compare(m_oFieldCache(iIndex).GetName(), sColumnName, StringComparison.CurrentCultureIgnoreCase) = 0 Then 

                Return iIndex 

            End If 

        Next 

        Throw New System.IndexOutOfRangeException(My.Resources.ColumnNameNotFound) 

    End Function 

#End Region 

#Region " Get methods based on the column ordinal " 

    ' We just pass all of these on to the field cache. 

    Public Overrides Function GetBoolean(ByVal iOrdinal As Integer) As Boolean 

        Return GetField(iOrdinal).GetBoolean() 

    End Function 

    Public Overrides Function GetByte(ByVal iOrdinal As Integer) As Byte 

        Return GetField(iOrdinal).GetByte() 

    End Function 

    Public Overrides Function GetBytes(ByVal iOrdinal As Integer, ByVal lDataOffset As Long, ByVal yBuffer() As Byte, ByVal iBufferOffset As Integer, ByVal iLength As Integer) As Long 

        Return GetField(iOrdinal).GetBytes(lDataOffset, yBuffer, iBufferOffset, iLength) 

    End Function 

    Public Overrides Function GetChar(ByVal iOrdinal As Integer) As Char 

        Return GetField(iOrdinal).GetChar() 

    End Function 

    Public Overrides Function GetChars(ByVal iOrdinal As Integer, ByVal lDataOffset As Long, ByVal yBuffer() As Char, ByVal iBufferOffset As Integer, ByVal iLength As Integer) As Long 

        Return GetField(iOrdinal).GetChars(lDataOffset, yBuffer, iBufferOffset, iLength) 

    End Function 

    Public Overrides Function GetDateTime(ByVal iOrdinal As Integer) As Date 

        Return GetField(iOrdinal).GetDateTime() 

    End Function 

    Public Overrides Function GetDecimal(ByVal iOrdinal As Integer) As Decimal 

        Return GetField(iOrdinal).GetDecimal() 

    End Function 

    Public Overrides Function GetDouble(ByVal iOrdinal As Integer) As Double 

        Return GetField(iOrdinal).GetDouble() 

    End Function 

    Public Overrides Function GetFloat(ByVal iOrdinal As Integer) As Single 

        Return GetField(iOrdinal).GetFloat() 

    End Function 

    Public Overrides Function GetGuid(ByVal iOrdinal As Integer) As System.Guid 

        Return GetField(iOrdinal).GetGuid() 

    End Function 

    Public Overrides Function GetInt16(ByVal iOrdinal As Integer) As Short 

        Return GetField(iOrdinal).GetInt16() 

    End Function 

    Public Overrides Function GetInt32(ByVal iOrdinal As Integer) As Integer 

        Return GetField(iOrdinal).GetInt32() 

    End Function 

    Public Overrides Function GetInt64(ByVal iOrdinal As Integer) As Long 

        Return GetField(iOrdinal).GetInt64() 

    End Function 

    Public Overrides Function GetString(ByVal iOrdinal As Integer) As String 

        Return GetField(iOrdinal).GetString() 

    End Function 

    Public Overrides Function GetValue(ByVal iOrdinal As Integer) As Object 

        Return GetField(iOrdinal).GetValue() 

    End Function 

    Default Public Overloads Overrides ReadOnly Property Item(ByVal iOrdinal As Integer) As Object 

        Get 

            Return Me.GetValue(iOrdinal) 

        End Get 

    End Property 

#End Region 

#Region " Other public methods " 

    Public Overrides Function GetValues(ByVal yValues() As Object) As Integer 

        ' Gets all values for the current row. Returns the number of instances of Object in the array. 

        Dim iIndex As Integer, iCount As Integer 

        If (yValues IsNot Nothing) AndAlso (yValues.Length > 0) AndAlso (m_iFieldCount > 0) Then 

            iCount = System.Math.Min(yValues.Length, m_iFieldCount) 

            For iIndex = 0 To iCount - 1 

                yValues(iIndex) = Me.GetValue(iIndex) 

            Next 

            Return iCount 

        Else 

            Return 0 

        End If 

    End Function 

    Public Overrides Function GetEnumerator() As System.Collections.IEnumerator 

        Return New DbEnumerator(Me) 

    End Function 

#End Region 

#Region " Unimplemented properties and methods " 

    Public Overrides ReadOnly Property Depth() As Integer 

        Get 

            ' We don't implement this. 

            Return 0 

        End Get 

    End Property 

    Public Overrides ReadOnly Property RecordsAffected() As Integer 

        Get 

            ' Gets the number of rows changed, inserted, or deleted by execution of the SQL statement. Returns the number of rows 
            ' changed, inserted, or deleted; 0 if no rows were affected or the statement failed; and -1 for SELECT statements. 

            ' In Jet, action queries don't return Recordsets, so we always return -1. 

            Return -1 

        End Get 

    End Property 

    Public Overrides Function NextResult() As Boolean 

        ' Advances the reader to the next result when reading the results of a batch of statements. 
        ' This method allows you to process multiple result sets returned when a batch is submitted to the data provider. 

        Throw New System.NotImplementedException 

    End Function 

    Public Overrides Function GetSchemaTable() As DataTable 

        Throw New System.NotImplementedException 

    End Function 

#End Region 

#Region " Private Routines " 

    Private Function GetField(ByVal iOrdinal As Integer) As JetField 

        AssertReaderIsOpen() 

        AssertRecordIsReady() 

        ValidateOrdinal(iOrdinal) 

        Return m_oFieldCache(iOrdinal) 

    End Function 

    Private Sub AssertReaderIsOpen() 

        If Me.IsClosed Then 

            Throw New System.InvalidOperationException(My.Resources.DataReaderClosed) 

        End If 

    End Sub 

    Private Sub AssertRecordIsReady() 

        ' See Read() for notes on .BOF and .EOF. 

        If Not m_bRecordIsReady Then 

            Throw New System.InvalidOperationException(My.Resources.RecordNotAvailable) 

        End If 

    End Sub 

    Private Sub ValidateOrdinal(ByVal iOrdinal As Integer) 

        If (iOrdinal < 0) Or (iOrdinal >= m_iFieldCount) Then 

            Throw New System.IndexOutOfRangeException(My.Resources.OrdinalOutOfRange) 

        End If 

    End Sub 

#End Region 

End Class



 
Imports System.Data ' For access to DbType 

Friend Class JetTypeInfo 

    ' TODO: Nothing 

    ' Each instance of this class represents a data type used by Jet. It returns various bits of information about that type.
    ' Because there are a fixed number of types used by Jet, we pre-build the JetTypeInfo instances for each type using a
    ' shared constructor that is called the first time this class is accessed. Those instances are stored in private shared
    ' class members, which are returned by the FromXyz() methods, which return one of our pre-built JetTypeInfo instances
    ' that correspond to a DbType, DaoType, etc. 

#Region " Shared class constructor, variables and methods " 

#Region " Shared class variables that hold the pre-built instances of JetTypeInfo objects " 

    Private Shared m_oBinary As JetTypeInfo
    Private Shared m_oBinaryBig As JetTypeInfo
    Private Shared m_oBinaryMax As JetTypeInfo
    Private Shared m_oBoolean As JetTypeInfo
    Private Shared m_oByte As JetTypeInfo
    Private Shared m_oCurrency As JetTypeInfo
    Private Shared m_oDateTime As JetTypeInfo
    Private Shared m_oDecimal As JetTypeInfo
    Private Shared m_oDouble As JetTypeInfo
    Private Shared m_oGuid As JetTypeInfo
    Private Shared m_oInt16 As JetTypeInfo
    Private Shared m_oInt32 As JetTypeInfo
    Private Shared m_oSingle As JetTypeInfo
    Private Shared m_oString As JetTypeInfo
    Private Shared m_oStringMax As JetTypeInfo 

    Private Shared m_oDefault As JetTypeInfo 

#End Region 

#Region " Shared class constructor that pre-builds the instances of JetTypeInfo objects " 

    Shared Sub New() 

        ' This is the shared class constructor that pre-builds the instances of JetTypeInfo objects that are
        ' return by the FromXyz() methods below. 

        ' This is a "shared constructor" that is called the first time anyone does anything with the class.
        ' See http://forums.devx.com/showthread.php?t=54792 for information. 

        m_oBinary = New JetTypeInfo(JetType.Binary, DAO.DataTypeEnum.dbBinary, DbType.Binary, GetType(Byte()), "Binary")
        m_oBinaryBig = New JetTypeInfo(JetType.BinaryBig, DAO.DataTypeEnum.dbVarBinary, DbType.Binary, GetType(Byte()), "BinaryBig")
        m_oBinaryMax = New JetTypeInfo(JetType.BinaryMax, DAO.DataTypeEnum.dbLongBinary, DbType.Binary, GetType(Byte()), "BinaryMax")
        m_oBoolean = New JetTypeInfo(JetType.Boolean, DAO.DataTypeEnum.dbBoolean, DbType.Boolean, GetType(Boolean), "Boolean")
        m_oByte = New JetTypeInfo(JetType.Byte, DAO.DataTypeEnum.dbByte, DbType.Byte, GetType(Byte), "Byte")
        m_oCurrency = New JetTypeInfo(JetType.Currency, DAO.DataTypeEnum.dbCurrency, DbType.Currency, GetType(Double), "Currency")
        m_oDateTime = New JetTypeInfo(JetType.DateTime, DAO.DataTypeEnum.dbDate, DbType.DateTime, GetType(DateTime), "DateTime")
        m_oDecimal = New JetTypeInfo(JetType.Decimal, DAO.DataTypeEnum.dbDecimal, DbType.Decimal, GetType(Double), "Decimal")
        m_oDouble = New JetTypeInfo(JetType.Double, DAO.DataTypeEnum.dbDouble, DbType.Double, GetType(Double), "Double")
        m_oGuid = New JetTypeInfo(JetType.Guid, DAO.DataTypeEnum.dbGUID, DbType.Guid, GetType(System.Guid), "Guid")
        m_oInt16 = New JetTypeInfo(JetType.Int16, DAO.DataTypeEnum.dbInteger, DbType.Int16, GetType(Int16), "Int16")
        m_oInt32 = New JetTypeInfo(JetType.Int32, DAO.DataTypeEnum.dbLong, DbType.Int32, GetType(Int32), "Int32")
        m_oSingle = New JetTypeInfo(JetType.Single, DAO.DataTypeEnum.dbSingle, DbType.Single, GetType(Single), "Single")
        m_oString = New JetTypeInfo(JetType.String, DAO.DataTypeEnum.dbText, DbType.String, GetType(String), "String")
        m_oStringMax = New JetTypeInfo(JetType.StringMax, DAO.DataTypeEnum.dbMemo, DbType.String, GetType(String), "StringMax") 

        m_oDefault = m_oString 

    End Sub 

#End Region 

#Region " Shared methods that return pre-built JetTypeInfo instances " 

    Public Shared Function FromDaoType(ByVal iDaoType As Short) As JetTypeInfo 

        Select Case DirectCast(Convert.ToInt32(iDaoType), DAO.DataTypeEnum)
            Case DAO.DataTypeEnum.dbBinary
                Return m_oBinary
            Case DAO.DataTypeEnum.dbBoolean
                Return m_oBoolean
            Case DAO.DataTypeEnum.dbByte
                Return m_oByte
            Case DAO.DataTypeEnum.dbCurrency
                Return m_oCurrency
            Case DAO.DataTypeEnum.dbDate
                Return m_oDateTime
            Case DAO.DataTypeEnum.dbDecimal
                Return m_oDecimal
            Case DAO.DataTypeEnum.dbDouble
                Return m_oDouble
            Case DAO.DataTypeEnum.dbGUID
                Return m_oGuid
            Case DAO.DataTypeEnum.dbInteger
                Return m_oInt16
            Case DAO.DataTypeEnum.dbLong
                Return m_oInt32
            Case DAO.DataTypeEnum.dbLongBinary
                Return m_oBinaryMax
            Case DAO.DataTypeEnum.dbMemo
                Return m_oStringMax
            Case DAO.DataTypeEnum.dbSingle
                Return m_oSingle
            Case DAO.DataTypeEnum.dbText
                Return m_oString
            Case DAO.DataTypeEnum.dbVarBinary
                Return m_oBinaryBig
            Case Else
                ' This includes the DAO types that Jet does not support:dbChar, dbFloat, dbBigInt, dbNumeric, dbTime, dbTimeStamp
                Throw New System.ArgumentException(My.Resources.InvalidDaoType)
        End Select 

    End Function 

    Public Shared Function FromDbType(ByVal eDbType As DbType) As JetTypeInfo 

        ' Note that "AnsiString" is suppossed to be non-Unicode. Jet supports Unicode based on the
        ' database version, not on the field type, so we treat both as the same thing. 

        ' DbType doesn't seem to have any concept of a Memo or VarChar(max) type field, nor the same thing for binary either. 

        Select Case eDbType
            Case DbType.AnsiString
                Return m_oString
            Case DbType.AnsiStringFixedLength
                Return m_oString
            Case DbType.Binary
                Return m_oBinary
            Case DbType.Boolean
                Return m_oBoolean
            Case DbType.Byte
                Return m_oBoolean
            Case DbType.Currency
                Return m_oCurrency
            Case DbType.Date
                Return m_oDateTime
            Case DbType.DateTime
                Return m_oDateTime
            Case DbType.Decimal
                Return m_oDecimal
            Case DbType.Double
                Return m_oDouble
            Case DbType.Guid
                Return m_oGuid
            Case DbType.Int16
                Return m_oInt16
            Case DbType.Int32
                Return m_oInt32
            Case DbType.Single
                Return m_oSingle
            Case DbType.String
                Return m_oString
            Case DbType.StringFixedLength
                Return m_oString
            Case DbType.Time
                Return m_oDateTime
            Case Else
                ' This includes unsupported DbType values: Int64, Object, SByte, UInt16, UInt32, UInt64, VarNumeric, Xml
                Throw New System.ArgumentException(My.Resources.InvalidDbType)
        End Select 

    End Function 

    Public Shared Function FromJetType(ByVal eType As JetType) As JetTypeInfo 

        Select Case eType
            Case JetType.Binary
                Return m_oBinary
            Case JetType.BinaryBig
                Return m_oBinaryBig
            Case JetType.BinaryMax
                Return m_oBinaryMax
            Case JetType.Boolean
                Return m_oBoolean
            Case JetType.Byte
                Return m_oByte
            Case JetType.Currency
                Return m_oCurrency
            Case JetType.DateTime
                Return m_oDateTime
            Case JetType.Decimal
                Return m_oDecimal
            Case JetType.Double
                Return m_oDouble
            Case JetType.Guid
                Return m_oGuid
            Case JetType.Int16
                Return m_oInt16
            Case JetType.Int32
                Return m_oInt32
            Case JetType.Single
                Return m_oSingle
            Case JetType.String
                Return m_oString
            Case JetType.StringMax
                Return m_oStringMax
            Case Else
                Throw New System.ArgumentException(My.Resources.InvalidJetType)
        End Select 

    End Function 

    Public Shared Function FromValue(ByVal oValue As Object) As JetTypeInfo 

        If (oValue Is Nothing) Or (System.Convert.IsDBNull(oValue)) Then 

            Return m_oDefault 

        Else 

            Dim oType As System.Type 

            oType = oValue.GetType() 

            Select Case System.Type.GetTypeCode(oType) 

                Case TypeCode.Object 

                    If oType Is GetType(Byte()) Then 

                        If DirectCast(oValue, Byte()).Length > 510 Then 

                            Return m_oBinaryMax 

                        Else 

                            Return m_oBinary 

                        End If 

                    ElseIf oType Is GetType(Guid) Then 

                        Return m_oGuid 

                    Else 

                        Throw New System.ArgumentException(My.Resources.InvalidSystemValueType) 

                    End If 

                Case TypeCode.String 

                    If DirectCast(oValue, String).Length > 255 Then 

                        Return m_oStringMax 

                    Else 

                        Return m_oString 

                    End If 

                Case TypeCode.Boolean
                    Return m_oBoolean
                Case TypeCode.Byte
                    Return m_oByte
                Case TypeCode.DateTime
                    Return m_oDateTime
                Case TypeCode.Decimal
                    Return m_oDecimal
                Case TypeCode.Double
                    Return m_oDouble
                Case TypeCode.Int16
                    Return m_oInt16
                Case TypeCode.Int32
                    Return m_oInt32
                Case TypeCode.Single
                    Return m_oSingle
                Case Else
                    ' This includes unsupported type codes: Char, DBNull, Empty, Int64, SByte, UInt16, UInt32, UInt64
                    Throw New System.ArgumentException(My.Resources.InvalidSystemValueType)
            End Select 

        End If 

    End Function 

    Public Shared ReadOnly Property [Default]() As JetTypeInfo 

        Get 

            Return m_oDefault 

        End Get 

    End Property 

#End Region 

#End Region 

#Region " Instance constructor, variables and methods " 

#Region " Private instance variables that contain information about the type the instance represents " 

    Private m_eJetType As JetType
    Private m_eDaoType As DAO.DataTypeEnum
    Private m_eDbType As DbType
    Private m_oSystemType As System.Type
    Private m_sName As String 

#End Region 

#Region " Private instance constructor " 

    Private Sub New(ByVal eJetType As JetType, ByVal eDaoType As DAO.DataTypeEnum, ByVal eDbType As DbType, ByVal oSystemType As System.Type, ByVal sName As String) 

        ' This private constructor is used only by the shared class constructor above to create the pre-built instances. 

        m_eJetType = eJetType
        m_eDaoType = eDaoType
        m_eDbType = eDbType
        m_oSystemType = oSystemType
        m_sName = sName 

    End Sub 

#End Region 

#Region " Public properties for JetTypeInfo instances " 

    Public ReadOnly Property JetType() As JetType 

        Get 

            Return m_eJetType 

        End Get 

    End Property 

    Public ReadOnly Property DaoType() As DAO.DataTypeEnum 

        Get 

            Return m_eDaoType 

        End Get 

    End Property 

    Public ReadOnly Property DbType() As DbType 

        Get 

            Return m_eDbType 

        End Get 

    End Property 

    Public ReadOnly Property SystemType() As System.Type 

        Get 

            Return m_oSystemType 

        End Get 

    End Property 

    Public ReadOnly Property Name() As String 

        Get 

            Return m_sName 

        End Get 

    End Property 

#End Region 

#Region " Public methods for JetTypeInfo instances " 

    Public Function CoerceValue(ByVal oValue As Object) As Object 

        ' This method attempts to convert the supplied value into the type represented by this JetTypeInfo object. 

        If (oValue Is Nothing) OrElse (System.Convert.IsDBNull(oValue)) Then 

            Return oValue 

        ElseIf oValue.GetType() Is Me.SystemType Then 

            Return oValue 

        Else 

            Try 

                Return System.Convert.ChangeType(oValue, Me.SystemType) 

            Catch oException As Exception 

                Throw New InvalidCastException(My.Resources.ValueConversionFailed, oException) 

            End Try 

        End If 

    End Function 

#End Region 

#End Region 

End Class

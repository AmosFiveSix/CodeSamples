Option Explicit

' I wrote this back in 1999-2001 to serialize complex object models to XML files in VB6.

'*************************************************************************************
'*
'*  XmlPropertyBag
'*
'*************************************************************************************
'
'    Revision History:
'
'      January 30, 2001 - Thomas J. Winter - Initial version
'
''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''

' This object works like Visual Basic's property bag, only it uses the XML file format. It also adds
' InitProperty and ClearProperty to help manage a child/parent relationship between objects.

' Calls the to object can get very recursive. Writing the sample XML file below would create this call stack:
'
' StationerySet.Save calls...
'  WriteProperty("StationerySet", Me), which calls...
'    WriteVariant, which calls...
'      WriteObject, which calls...
'        StationerySet.WriteProperties, which calls...
'          WriteProperty("Name", "..."), which calls...
'            WriteVariant, which calls...
'              WriteSimpleType
'          WriteProperty("Suggestions", m_oSuggestions), whic calls...
'            WriteVariant, which calls...
'              WriteObject, which calls...
'                Suggestions.WriteProperties, which calls...
'                  WriteProperty("PaperSize", ...)
'                  ...
'                  WriteProperty("Sender", m_oSender), which calls...
'                    WriteVariant, which calls...
'                      WriteObject, which calls...
'                        Sender.WriteProperties, which calls...
'                          WriteProperty("FullName", "...")
'                          ...
'                  WriteProperty("WideMargins", m_oWideMargins)
'                    WriteVariant, which calls...
'                      WriteObject, which calls...
'                        Sender.WriteProperties, which calls...
'                          WriteProperty("Active", ...)
'                          ...
'
' Each WriteObject adds a new Element to the XML document. What Element is written to is kept track
' of by the m_oElementStack.
'
' Reading, Initing and Clearing work similarly.
'
' Here is a sample XML file created by this object.
'
'  <?xml version="1.0" encoding="UTF-16" standalone="yes" ?>
'  <XmlToObj xmlns:dt="urn:schemas-microsoft-com:datatypes" xml:space="default">
'    <StationerySet Class="Stationery.StationerySet" Key="The Sample Company">
'      <Name dt:dt="string">The Sample Company</Name>
'      <Suggestions Class="Stationery.Suggestions">
'        <PaperSize dt:dt="i4">1</PaperSize>
'        <Language dt:dt="string">English (U.S.)</Language>
'        <Artwork dt:dt="string" />
'        <Sender Class="Stationery.Sender">
'          <FullName dt:dt="string">Your Name</FullName>
'          <JobTitle dt:dt="string">Your Title</JobTitle>
'          <PrimaryAddress dt:dt="string">1835 Dueber Avenue, S.W.?P.O. Box 6927?Canton, OH 44706-0927 U.S.A.</PrimaryAddress>
'          <SecondaryAddress dt:dt="string" />
'          <MailCode dt:dt="string">ABC-00</MailCode>
'          <Telephone dt:dt="string">(330) 471-0000</Telephone>
'          <Facsimile dt:dt="string">(330) 471-0000</Facsimile>
'          <Email dt:dt="string">userid@sample.com</Email>
'          <TollFree dt:dt="string" />
'          <Mobile dt:dt="string" />
'          <Pager dt:dt="string" />
'          <Uses dt:dt="i4">0</Uses>
'        </Sender>
'        <WideMargins Class="Stationery.WideMargins">
'          <Active dt:dt="boolean">0</Active>
'          <Left dt:dt="r4">-18</Left>
'          <Right dt:dt="r4">18</Right>
'          <Top dt:dt="r4">9</Top>
'          <Bottom dt:dt="r4">18</Bottom>
'        </WideMargins>
'      </Suggestions>
'    </StationerySet>
'  </XmlToObj>

'********************************************************************************
'**                                Public Events                               **
'********************************************************************************

' This event is raised to allow the client to create any objects needed when
' reading or initing.

Public Event CreateObject(ByVal sClass As String, ByRef oObject As Object)

' This event is raised when any objects are cleared.

Public Event DeleteObject(ByVal oObject As Object)

'********************************************************************************
'**                             Public Enumerations                            **
'********************************************************************************

Public Enum xtoErrors

    xtoErrInternal = vbObjectError + &H6500
    
    xtoErrNoMemory
    
    xtoErrCannotRead
    xtoErrNoReadSource
    xtoErrNoDocument
    xtoErrParse
    xtoErrWrongFormat
    xtoErrRead
    xtoErrNoSuchProperty
    xtoErrCreateObject
    xtoErrNotPersistable
    
    xtoErrCannotWrite
    xtoErrPropertyExists
    xtoErrWrite
    xtoErrUnknownType
    
    xtoErrCannotInit
    xtoErrNotCreatable
    
    xtoErrCannotClear
    
    xtoErrSave
    xtoErrCannotSave
    
    xtoErrGetContents
    xtoErrCannotGetContents
    
    xtoErrCannotReset
    xtoErrCannotSetParent
    
    xtoErrInitialize
    xtoErrFatal
    
    xtoErrFileNotFound
    
    xtoErrExternal

End Enum

'********************************************************************************
'**                         Private Member Variables                           **
'********************************************************************************

Private Err As ErrorHandler

Private m_eState As xtoState

Private m_lDepth As Long

Private m_oDocument As MSXML.DOMDocument

Private m_oElementStack As Stack

Private m_oParentStack As Stack

Private m_oParent As IXmlParent

'********************************************************************************
'**                              Public Properties                             **
'********************************************************************************

Public Property Get Contents() As String

    On Error GoTo ErrorHandler
        
        ' First we have to make sure we're in a state where we have some contents to get.
        
        If (m_eState = xtoHaveWritten) Or (m_eState = xtoWriting) Or (m_eState = xtoReadyToRead) Or (m_eState = xtoReading) Then
            
            Contents = GetDocumentXml()
            
        Else
            
            Err.Raise xtoErrCannotGetContents
            
        End If
        
    Exit Property
    
ErrorHandler:
    
    Err.Handle

End Property

Public Property Let Contents(sData As String)

    On Error GoTo ErrorHandler
        
        ' This switches us to reading so we have to make sure we're not doing something else right now.
        
        If (m_eState = xtoReading) Or (m_eState = xtoWriting) Or (m_eState = xtoIniting) Or (m_eState = xtoClearing) Then
            
            Err.Raise xtoErrCannotRead
            
        End If
        
        If (sData = "") Or ((m_eState = xtoReadyToRead) Or (m_eState = xtoHaveWritten) Or (m_eState = xtoHaveInited) Or (m_eState = xtoHaveCleared)) Then
            
            Me.Reset
            
        End If
        
        If sData <> "" Then
            
            StartReading xtoReadSourceData, sData
            
        End If
        
    Exit Property
    
ErrorHandler:
    
    Err.Handle

End Property

Public Property Get Parent() As IXmlParent

    On Error GoTo ErrorHandler
        
        If Not m_oParentStack.IsEmpty Then
            
            Set Parent = m_oParentStack.Last
            
        Else
            
            Set Parent = m_oParent
            
        End If
        
    Exit Property
    
ErrorHandler:
    
    Err.Handle

End Property

Public Property Set Parent(oParent As IXmlParent)

    On Error GoTo ErrorHandler
        
        ' You can only set this property before you begin reading or initing.
        
        If (m_eState = xtoReading) Or (m_eState = xtoWriting) Or (m_eState = xtoIniting) Or (m_eState = xtoClearing) Then
            
            Err.Raise xtoErrCannotSetParent
            
        End If
        
        ' This removes any previous parent that was set.
        
        If (m_eState = xtoReadyToRead) Or (m_eState = xtoHaveInited) Then
            
            m_oParentStack.Reset
            
        End If
        
        Set m_oParent = oParent
        
        If Not m_oParentStack Is Nothing Then
            
            m_oParentStack.Push m_oParent
            
        End If
        
    Exit Property
    
ErrorHandler:
    
    Err.Handle

End Property

Public Property Get Class(PropertyName As String) As String

    On Error GoTo ErrorHandler
        
        Dim oElement As MSXML.IXMLDOMElement
        
        ' This makes sure that we are reading right now.
        
        Call PrepForReading
        
        Set oElement = GetChildElement(m_oElementStack.Last, PropertyName)
        
        If Not oElement Is Nothing Then
            
            Class = GetElementAttr(oElement, xtoClass)
            
        End If
        
    Exit Property
    
ErrorHandler:
    
    Err.Handle

End Property

Public Property Get Key(PropertyName As String) As String

    On Error GoTo ErrorHandler
        
        Dim oElement As MSXML.IXMLDOMElement
        
        ' This makes sure that we are reading right now.
        
        Call PrepForReading
        
        Set oElement = GetChildElement(m_oElementStack.Last, PropertyName)
        
        If Not oElement Is Nothing Then
            
            Key = GetElementAttr(oElement, xtoKey)
            
        End If
        
    Exit Property
    
ErrorHandler:
    
    Err.Handle

End Property

'********************************************************************************
'**                               Public Methods                               **
'********************************************************************************

Public Sub Load(sFile As String)

    On Error GoTo ErrorHandler
        
        If Not FileExists(sFile) Then
            
            Err.Raise xtoErrFileNotFound
            
        End If
        
        ' This makes sure we're not writing or something like that.
        
        Call PrepForLoading
        
        StartReading xtoReadSourceFile, sFile
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Public Sub Save(sFile As String)

    On Error GoTo ErrorHandler
        
        If (m_eState = xtoHaveWritten) Or (m_eState = xtoWriting) Or (m_eState = xtoReadyToRead) Or (m_eState = xtoReading) Then
            
            SaveXmlDocument sFile
            
        Else
            
            Err.Raise xtoErrCannotSave
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Public Sub Reset()

    On Error GoTo ErrorHandler
        
        If (m_eState = xtoReading) Or (m_eState = xtoWriting) Or (m_eState = xtoIniting) Or (m_eState = xtoClearing) Then
            
            Err.Raise xtoErrCannotReset
            
        End If
        
        ' StopCurrentState doesn't do this so we have to do it here.
        
        Set m_oParent = Nothing
        
        Call StopCurrentState
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Public Function ReadProperty(Optional PropertyName As Variant, Optional DefaultValue As Variant, Optional ByRef Argument As Variant, Optional ByRef ObjectKey As Variant) As Variant

    On Error GoTo ErrorHandler
        
        Err.Try
            
            ' This makes sure we're not in the middle of writing, initing, or clearing,
            ' and that we have something to read from. If not, it raises an error.
            
            Call PrepForReading
            
            If Err.Success Then
                
                ' Since the client will make recursive calls to this routine, we keep track of
                ' how many times they have.
                
                IncreaseState xtoReading
                
                If Err.Success Then
                    
                    ReadVariant ReadProperty, PropertyName, DefaultValue, Argument, ObjectKey
                    
                    ' This checks to see if we have come back from recursive calls to the original
                    ' call to this routine. If so, we set the current state to indicate that
                    ' we are not in the middle of reading.
                    
                    DecreaseState xtoReadyToRead
                    
                End If
                
            End If
            
        Err.Finally
            
    Exit Function
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Function

Public Sub WriteProperty(PropertyName As String, PropertyValue As Variant, Optional DefaultValue As Variant)

    ' Note that we ignore the DefaultValue parameter.
    
    On Error GoTo ErrorHandler
        
        Err.Try
            
            ' This makes sure that the client is not already using this object to read, init or clear.
            ' If so, it raises an error. It also clears out any content that might be left over from
            ' a previous read operation. It then sets up a new XML document for us to write to.
            
            Call PrepForWriting
            
            If Err.Success Then
                
                ' Since the client will likely be calling this routine recursively from within
                ' IXmlPersistable.WriteProperties, we keep track of how deep we can know when
                ' we finally come out of it all.
                
                IncreaseState xtoWriting
                
                If Err.Success Then
                    
                    WriteVariant PropertyName, PropertyValue
                    
                    ' This switches our state if we've finally come back from all our recusive writes.
                    
                    DecreaseState xtoHaveWritten
                    
                End If
                
            End If
            
        Err.Finally
        
    Exit Sub
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Sub

Public Function InitProperty(Class As String, Optional ByRef Argument As Variant) As Object

    On Error GoTo ErrorHandler
        
        Err.Try
            
            ' This makes sure that the client is not already using this object to read, write or clear.
            ' If so, it raises an error. It also clears out any content that might be left over from
            ' a previous read or write operation. It then sets us up to start Initing.
            
            Call PrepForIniting
            
            If Err.Success Then
                
                ' Since the client will likely be calling this routine recursively from within
                ' IXmlCreatable.InitProperties, we keep track of how deep we can know when
                ' we finally come out of it all.
                
                IncreaseState xtoIniting
                
                If Err.Success Then
                    
                    Set InitProperty = InitObject(Class, Argument)
                    
                    ' This switches our state if we've finally come back from all our recusive inits.
                    
                    DecreaseState xtoHaveInited
                    
                End If
                
            End If
            
        Err.Finally
        
    Exit Function
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Function

Public Sub ClearProperty(PropertyValue As Variant)

    On Error GoTo ErrorHandler
        
        Err.Try
            
            ' This makes sure that the client is not already using this object to read, write or init.
            ' If so, it raises an error. It also clears out any content that might be left over from
            ' a previous read or write operation. It then sets us up to start clearing.
            
            Call PrepForClearing
            
            If Err.Success Then
                
                ' Since the client will likely be calling this routine recursively from within
                ' IXmlCreatable.ClearProperties, we keep track of how deep we can know when
                ' we finally come out of it all.
                
                IncreaseState xtoClearing
                
                If Err.Success Then
                    
                    ClearVariant PropertyValue
                    
                    ' This switches our state if we've finally come back from all our recusive inits.
                    
                    DecreaseState xtoHaveCleared
                    
                End If
                
            End If
            
        Err.Finally
        
    Exit Sub
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Sub

'********************************************************************************
'**                            Private Read Methods                            **
'********************************************************************************

Private Sub ReadVariant(vResult As Variant, Optional vName As Variant, Optional vDefault As Variant, Optional ByRef vArgument As Variant, Optional ByRef vObjectKey As Variant)

    On Error GoTo ErrorHandler
    
        Dim oElement As MSXML.IXMLDOMElement
        
        Dim sClass As String
        
        ' This gets the named XML element that is a child of the last element placed on the stack.
        ' At the beginning of a read, the last element on the stack will be the "XmlToObj" root
        ' element. (This is the document element. See StartReading.) So GetChildElement would return
        ' elements that are children of this root element. If this child element turns out to represent an
        ' object, then this child element will be placed on the stack and its ReadProperties method
        ' will be called so it can into this function and read its child elements.
        
        Set oElement = GetChildElement(m_oElementStack.Last, IIf(IsMissing(vName), "", CStr(vName)))
        
        If Not oElement Is Nothing Then
            
            ' When writing objects, we write a "Class" attribute in the XML that tells us what
            ' the class of the object is. If there is not "Class" attribute, then we know we
            ' have a simple variant type. "Empty", "Nothing", and "Collection" are three classes
            ' that we have built-in special handling for.
            
            sClass = GetElementAttr(oElement, xtoClass)
            
            Select Case sClass
                
                Case Is = ""
                    
                    ReadSimpleType vResult, oElement
                    
                Case xtoEmptyClass
                    
                    vResult = Empty
                    
                Case xtoNothingClass
                    
                    Set vResult = Nothing
                    
                Case xtoCollectionClass
                    
                    ReadCollection vResult, oElement, vArgument
                    
                Case Else
                    
                    ReadObject vResult, oElement, sClass, vArgument, vObjectKey
                    
            End Select
            
        Else
            
            ' We get here is we could not find a child element with the name that the client
            ' was looking for. So we return the default value if one was provided.
            
            If Not IsMissing(vDefault) Then
                
                CopyVariant vResult, vDefault
                
            Else
                
                Err.Raise xtoErrNoSuchProperty
                
            End If
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub ReadCollection(vResult As Variant, oElement As MSXML.IXMLDOMElement, Optional ByRef vArgument As Variant)

    ' This routine is called by ReadVariant when the class for the oElement is "Collection". We know
    ' that a standard Visual Basic collection has been written to the XML, so we create a new
    ' one and then read in all the items.
    
    On Error GoTo ErrorHandler
        
        Err.Try
            
            Dim oCollection As Collection
            
            Set oCollection = New Collection
            
            If Err.Success Then
                
                ' We are going to read the child elements of the Collection's element, so we have
                ' to push it onto the element stack.
                
                m_oElementStack.Push oElement
                
                If Err.Success Then
                    
                    ' This routine will read all of the elements and add them to the collection.
                    
                    ReadCollectionItems oElement, oCollection, vArgument
                    
                    m_oElementStack.Pop
                    
                End If
                
            End If
            
            If Err.Failure Then
                
                ' If something above failed, such as ReadCollectionItems, then we want to destory
                ' the collection we created and items that might have been added to it. ClearVariant
                ' will do this for us. Err.Finally will then raise the error for us.
                
                ClearVariant oCollection
                
                Set oCollection = Nothing
                
            End If
            
            Set vResult = oCollection
            
        Err.Finally
        
    Exit Sub
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Sub

Private Sub ReadCollectionItems(oElement As MSXML.IXMLDOMElement, oCollection As Collection, Optional ByRef vArgument As Variant)

    ' This routine will read all of the elements and add them to the collection.
    ' oElement is the element for the collection.
    
    On Error GoTo ErrorHandler
        
        Dim oChildNode As MSXML.IXMLDOMNode
        
        Dim oChildElement As MSXML.IXMLDOMElement
        
        Dim vItem As Variant
        
        Dim sKey As String
        
        ' We have to iterate through all of the child nodes of the Collection element (oElement).
        
        For Each oChildNode In oElement.childNodes
            
            ' Since XML nodes can be many things other than Elements (such as whitespace), we call
            ' NodeToElement, which only returns an element object is the current node is an element)
            
            Set oChildElement = NodeToElement(oChildNode)
            
            If Not oChildElement Is Nothing Then
                
                ' This child element could be anything, so we call ReadVariant to read it in.
                ' vItem will be set to whatever is read in. If its an object,  sKey will be set
                ' to whatever "Key" attribute might have been written for the object.
                ' See MakeElementForObject().
                
                ReadVariant vItem, oChildElement.nodeName, , vArgument, sKey
                
                If sKey = "" Then
                    
                    oCollection.Add vItem
                    
                Else
                    
                    oCollection.Add vItem, sKey
                    
                End If
                
                ' We make sure we don't keep any references to objects longer than we need to.
                ' It might mess up our error handling if this object we might be holding
                ' terminates at the wrong time.
                
                vItem = Empty
                
            End If
            
        Next
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Function ReadObject(vResult As Variant, oElement As MSXML.IXMLDOMElement, sClass As String, Optional ByRef vArgument As Variant, Optional ByRef vObjectKey As Variant) As Variant

    ' We know that oElement is for an object that was written to the XML.
    ' We have to set vResult to the object we create, and vObjectKey to any key that might have
    ' been written to the XML file for the object. (See MakeElementForObject()).
    
    On Error GoTo ErrorHandler
        
        Err.Try
            
            Dim oObject As Object
            
            ' Now we try to create an object with the given class and make sure it supports
            ' the IXmlPersistable interface.
            
            Set oObject = MakeObject(sClass, xtoObjectPersistable)
            
            If Err.Success Then
                
                ' If this object is a parent object, then we push it onto the parent stack so that
                ' any of its children can get a reference to it. See MakeObject() and SetObjectParent().
                
                If IsParent(oObject) Then m_oParentStack.Push oObject
                
                If Err.Success Then
                    
                    ' We are going to read the child elements of this object's element, so we
                    ' need to push it into the element stack. See ReadVariant().
                    
                    m_oElementStack.Push oElement
                    
                    If Err.Success Then
                        
                        ' This calls the ReadProperties method of the object's IXmlPersistable
                        ' interface. This object may then call back into our objects ReadProperty
                        ' method.
                        
                        CallReadProperties CastToPersistable(oObject), vArgument
                        
                        m_oElementStack.Pop
                        
                    End If
                    
                    ' Don't forget to take it off the parent stack as well.
                    
                    If IsParent(oObject) Then m_oParentStack.Pop
                    
                End If
                
            End If
            
            ' Note that even if we could not create the object, we still try to get the key for it.
            ' This routine will set vObjectKey appropriately.
            
            GetElementKey oElement, vObjectKey
            
            If Err.Failure Then
                
                ' If we had an error somewhere, we want to get rid of this object, but we need to
                ' clear it properly if it supports IXmlCreatable. ClearObject will do this for us.
                
                ClearObject oObject
                
                Set oObject = Nothing
                
            End If
            
            Set vResult = oObject
            
        Err.Finally
        
    Exit Function
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Function

Private Sub GetElementKey(oElement As MSXML.IXMLDOMElement, Optional vObjectKey As Variant)

    ' This routine is called by ReadObject. Is sets vObjectKey to the "Key" attribute that
    ' may have been written for this element. (See MakeElementForObject().) Keys are written
    ' for objects that are members or a collection.
    
    ' Note that vObjectKey is optional (as is the ObjectKey parameter of ReadProperty), so we
    ' have to check is its missing, meaning the client doesn't care about the key.
    
    On Error GoTo ErrorHandler
        
        If Not IsMissing(vObjectKey) Then
            
            vObjectKey = GetElementAttr(oElement, xtoKey)
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Exit Sub

End Sub

Private Sub ReadSimpleType(vResult As Variant, oElement As MSXML.IXMLDOMElement)

    ' This is called when the element (oElement) does not represent an object.
    
    On Error GoTo ErrorHandler
        
        Dim sDataType As String
        
        Dim vValue As Variant
        
        Dim sString As String
        
        ' This will set sDataType and vValue to the data type and value for this element.
        
        GetElementData oElement, sDataType, vValue
        
        ' We then do these conversions to make sure the value is in the correct range for VB.
        
        Select Case sDataType
            
            Case dtInteger
                
                vResult = CInt(vValue)
                
            Case dtLong
                
                vResult = CLng(vValue)
                
            Case dtSingle
                
                vResult = CSng(vValue)
                
            Case dtDouble
                
                vResult = CDbl(vValue)
                
            Case dtCurrency
                
                vResult = CCur(vValue)
                
            Case dtDecimal
                
                vResult = CDec(vValue)
                
            Case dtByte
                
                vResult = CByte(vValue)
                
            Case dtDate
                
                vResult = CDate(vValue)
                
            Case dtBoolean
                
                vResult = CBool(vValue)
                
            Case dtString
                
                sString = CStr(vValue)
                
                'sString = Mid$(sString, 2)
                'sString = Left$(sString, Len(sString) - 1)
                
                ' See WriteString for an explanation of this.
                
                sString = Replace$(sString, ChrW$(2029), vbCr)
                sString = Replace$(sString, ChrW$(2028), vbLf)
                
                vResult = sString
                
            Case Else
                
                Err.Raise xtoErrUnknownType
                
        End Select
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub PrepForReading()

    ' This is called by ReadProperty to make sure we're not in the middle of writing, initing,
    ' or clearing, and that we have something to read from. If not, it raises an error.
    
    On Error GoTo ErrorHandler
        
        If (m_eState = xtoWriting) Or (m_eState = xtoIniting) Or (m_eState = xtoClearing) Then
            
            Err.Raise xtoErrCannotRead
            
        End If
        
        If Not ((m_eState = xtoReadyToRead) Or (m_eState = xtoReading) Or (m_eState = xtoHaveWritten)) Then
            
            Err.Raise xtoErrNoReadSource
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub PrepForLoading()

    ' This is called by Load to basically make sure we're not already doing anything.
    
    On Error GoTo ErrorHandler
        
        If (m_eState = xtoReading) Or (m_eState = xtoWriting) Or (m_eState = xtoIniting) Or (m_eState = xtoClearing) Then
            
            Err.Raise xtoErrCannotRead
            
        End If
        
        If (m_eState = xtoReadyToRead) Or (m_eState = xtoHaveWritten) Or (m_eState = xtoHaveInited) Or (m_eState = xtoHaveCleared) Then
            
            Call StopCurrentState
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub StartReading(eSource As xtoReadSource, sSource As String)

    ' This is called by Let Content and Load to set up for reading an XML document.
    
    On Error GoTo ErrorHandler
        
        ' This creates the actual XML document object and loads in the XML.
        
        MakeDocumentForReading eSource, sSource
        
        Set m_oParentStack = New Stack
        
        If Not m_oParent Is Nothing Then
            
            m_oParentStack.Push m_oParent
            
        End If
        
        Set m_oElementStack = New Stack
        
        ' This pushes the root document element onto the element stack so we can read its children.
        ' See ReadVariant.
        
        m_oElementStack.Push m_oDocument.documentElement
        
        m_eState = xtoReadyToRead
        
        m_lDepth = 0
        
    Exit Sub
    
ErrorHandler:
    
    Err.Save
    
    Call StopCurrentState
    
    Err.Handle Saved:=True

End Sub

Private Sub MakeDocumentForReading(eSource As xtoReadSource, sSource As String)

    ' This create a new XML document object and loads in the XML from the appropriate source.
    
    On Error GoTo DocumentError
        
        Set m_oDocument = New MSXML.DOMDocument
        
    On Error GoTo ReadError
        
        With m_oDocument
            
            .async = False
            .preserveWhiteSpace = False
            .resolveExternals = False
            
        End With
        
    On Error GoTo ErrorHandler
        
        Select Case eSource
            
            Case xtoReadSourceData
                
                SetDocumentXml sSource
                
            Case xtoReadSourceFile
                
                LoadXmlDocument sSource
                
        End Select
        
        ' This makes sure that the root element is "XmlToObj".
        
        If m_oDocument.documentElement.nodeName <> xtoRoot Then
            
            Err.Raise xtoErrWrongFormat
            
        End If
        
    Exit Sub
        
DocumentError:
    
    Set m_oDocument = Nothing
    
    Err.Raise xtoErrNoDocument
    
ReadError:
    
    Set m_oDocument = Nothing
    
    Err.Raise xtoErrRead
    
ErrorHandler:
    
    Set m_oDocument = Nothing
    
    Err.Handle

End Sub

Private Sub GetElementData(oElement As MSXML.IXMLDOMElement, ByRef sDataType As String, ByRef vValue As Variant)

    ' Called by ReadSimpleType to retreive the data type and value. We make this a separate routine
    ' so that we can easily raise an xtoErrRead error if we have any problems with this.
    
    On Error GoTo ErrorHandler
        
        With oElement
            
            sDataType = .dataType
            
            vValue = .nodeTypedValue
            
        End With
        
    Exit Sub
    
ErrorHandler:
    
    Err.Raise xtoErrRead

End Sub

'********************************************************************************
'**                            Private Write Methods                           **
'********************************************************************************

Private Sub WriteVariant(sName As String, vValue As Variant)

    On Error GoTo ErrorHandler
        
        ' The XML element for this variant will be written as a child to the last element on the
        ' element stack. At the beginning of writting, the last element will be the "XmlToObj" element.
        
        ' First we have to make sure there is not already a child element with the name we want to use.
        
        If GetChildElement(m_oElementStack.Last, sName) Is Nothing Then
            
            ' Then we figure out how we want to write it.
            
            If IsObject(vValue) Then
                
                If vValue Is Nothing Then
                    
                    ' This writes a quick element with the "Class" attribute set to "Nothing"
                    
                    MakeElement sName, xtoNothingClass
                    
                ElseIf TypeName(vValue) = xtoCollectionClass Then
                    
                    WriteCollection sName, vValue
                    
                Else
                    
                    WriteObject sName, vValue
                    
                End If
                
            ElseIf IsEmpty(vValue) Then
                
                ' This writes a quick element with the "Class" attribute set to "Empty"
                
                MakeElement sName, xtoEmptyClass
                
            Else
                
                WriteSimpleType sName, vValue
                
            End If
            
        Else
            
            Err.Raise xtoErrPropertyExists
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Function WriteObject(sName As String, vObject As Variant)

    ' This routine is called when trying to write an object.
    
    On Error GoTo ErrorHandler
        
        Err.Try
            
            Dim oElement As MSXML.IXMLDOMElement
            
            ' This creates an XML element object, with its parent being the last element on the
            ' element stack. If we have just started writing, the last element on the element
            ' stack will be the "XmlToObj" element.
            
            Set oElement = MakeElementForObject(sName, vObject)
                    
            If Err.Success Then
                
                ' We will now need to write this object's properties, which means we'll have to write
                ' XML elements that are children of this element. So we push the element onto the stack.
                ' It is now the "last element" on the stack.
                
                m_oElementStack.Push oElement
                
                If Err.Success Then
                    
                    ' This call's the object's WriteProperties method.
                    
                    CallWriteProperties CastToPersistable(vObject)
                    
                    m_oElementStack.Pop
                    
                End If
                
            End If
            
            If Err.Failure Then
                
                ' If we have failure somewhere along the line, we want to remove the element
                ' and any children that might have been written.
                
                DeleteElement oElement
                
                Set oElement = Nothing
                
            End If
            
        Err.Finally
        
    Exit Function
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Function

Private Sub WriteCollection(sName As String, vCollection As Variant)

    ' The client is writing a Visual Basic collection object.
    
    On Error GoTo ErrorHandler
        
        Err.Try
            
            Dim oElement As MSXML.IXMLDOMElement
            
            ' We create element for the collection. Its "Class" attribute will be set to "Collection"
            
            Set oElement = MakeElement(sName, xtoCollectionClass)
            
            If Err.Success Then
                
                ' We will not need to write the items that are in the collection, which means we'll have
                ' to write XML elements that are children of this element. So we push the collection
                ' element onto the stack. It is now the "last element" on the stack.
                
                m_oElementStack.Push oElement
                
                If Err.Success Then
                    
                    WriteCollectionItems CastToCollection(vCollection)
                    
                    m_oElementStack.Pop
                    
                End If
                
            End If
            
            If Err.Failure Then
                
                ' If we have failure somewhere along the line, we want to remove the collection
                ' element and any items that might have been written.
                
                DeleteElement oElement
                
                Set oElement = Nothing
                
            End If
            
        Err.Finally
        
    Exit Sub
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Sub

Private Sub WriteCollectionItems(oCollection As Collection)

    ' Called by WriteCollection, it writes each item in the collection. Each of these items will
    ' be child elements of the collection element. Note element names cannot start with numbers,
    ' so we prepend "Item_" to the index of the item.
    
    On Error GoTo ErrorHandler
        
        Dim lIndex As Long
        
        With oCollection
            
            For lIndex = 1 To .Count
                
                WriteVariant xtoItemPrefix & LTrim$(Str$(lIndex)), .Item(lIndex)
                
            Next
            
        End With
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub WriteSimpleType(sName As String, vValue As Variant)

    ' We are writing a non object. Strings and Booleans require special handling.
    
    On Error GoTo ErrorHandler
        
        Dim oElement As MSXML.IXMLDOMElement
        
        Dim sDataType As String
        
        Select Case VarType(vValue)
            
            Case vbString
                
                WriteString sName, CStr(vValue)
                
            Case vbBoolean
                
                WriteBoolean sName, CBool(vValue)
                
            Case Else
                
                Select Case VarType(vValue)
                    
                    Case vbInteger: sDataType = dtInteger
                        
                    Case vbLong: sDataType = dtLong
                        
                    Case vbSingle: sDataType = dtSingle
                        
                    Case vbDouble: sDataType = dtDouble
                        
                    Case vbCurrency: sDataType = dtCurrency
                        
                    Case vbDecimal: sDataType = dtDecimal
                        
                    Case vbByte: sDataType = dtByte
                        
                    Case vbDate: sDataType = dtDate
                        
                    Case Else
                        
                        Err.Raise xtoErrUnknownType
                        
                End Select
                
                Set oElement = MakeElement(sName)
                
                SetElementData oElement, sDataType, vValue
                
        End Select
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub WriteString(sName As String, sString As String)

    On Error GoTo ErrorHandler
        
        Dim sNewString As String
        
        Dim oElement As MSXML.IXMLDOMElement
        
        sNewString = sString
        
        ' The XML rules state that CR's and the LF/CR combination are turned into just an LF.
        ' I want to preserve the string exactly, so I change CR's and LF's into their Unicode
        ' counterparts, which XML ignores. ReadSimpleType() undoes this operation.
        
        sNewString = Replace$(sNewString, vbLf, ChrW$(2028))
        sNewString = Replace$(sNewString, vbCr, ChrW$(2029))
        
        ' I had seen some problems with the whitespace handling of strings. Sometimes it seemed
        ' that MSXML wanted to strip out any beginning and ending spaces. I thought perhaps putting
        ' quotes around the string would help. After playing the the preserveWhitespace property
        ' (See MakeDocumentForWriting) it did not seem to be necessary.
        
        'sNewString = ChrW$(34) & sNewString & ChrW$(34)
        
        Set oElement = MakeElement(sName)
        
        SetElementData oElement, dtString, sNewString
        
    Exit Sub
    
ErrorHandler:
    
    Err.Raise xtoErrWrite

End Sub

Private Sub WriteBoolean(sName As String, bBoolean As Boolean)

    ' There is a bug in MSXML 2.0 that causes the boolean data type to be written wrong.
    ' If we simply set dataType to dtBoolean, then -1 would be written for .dataType. However,
    ' when reading the document, MSXML expects a 1 for True, and do will throw an error for the -1.
    ' Trust me. There's a KB article about it. We do this stuff to get around it. Changing the dataType
    ' fortunately does not reset the nodeTypedValue.
    
    On Error GoTo ErrorHandler
        
        Dim oElement As MSXML.IXMLDOMElement
        
        Set oElement = MakeElement(sName)
        
        With oElement
            
            .dataType = dtLong
            
            .nodeTypedValue = IIf(bBoolean = True, 1, 0)
            
            .dataType = dtBoolean
            
        End With
        
    Exit Sub
    
ErrorHandler:
    
    Err.Raise xtoErrWrite

End Sub

Private Sub PrepForWriting()

    ' This makes sure that the client is not already using this object to read, init or clear.
    ' If so, it raises an error. It also clears out any content that might be left over from
    ' a previous read operation. It then sets up a new XML document for us to write to.
    
    On Error GoTo ErrorHandler
        
        If (m_eState = xtoReading) Or (m_eState = xtoIniting) Or (m_eState = xtoClearing) Then
            
            Err.Raise xtoErrCannotWrite
            
        End If
        
        If (m_eState = xtoReadyToRead) Or (m_eState = xtoHaveInited) Or (m_eState = xtoHaveCleared) Then
            
            Call StopCurrentState
            
        End If
        
        If Not ((m_eState = xtoWriting) Or (m_eState = xtoHaveWritten)) Then
            
            Call StartWriting
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub StartWriting()
    
    ' Called by PrepForWriting.
    
    On Error GoTo ErrorHandler
        
        ' This sets up an XML document for writing (m_oDocument).
        
        Call MakeDocumentForWriting
        
        ' We don't worry about parents and children when writing.
        
        Set m_oParentStack = Nothing
        
        ' We push the root element ("XmlToObj") onto the element stack so that the next elements
        ' written will be its children.
        
        Set m_oElementStack = New Stack
        
        m_oElementStack.Push m_oDocument.documentElement
        
        m_eState = xtoHaveWritten
        
        m_lDepth = 0
        
    Exit Sub
    
ErrorHandler:
    
    Err.Save
    
    Call StopCurrentState
    
    Err.Handle Saved:=True

End Sub

Private Sub MakeDocumentForWriting()

    On Error GoTo DocumentError
        
        Set m_oDocument = New MSXML.DOMDocument
        
    On Error GoTo WriteError
        
        With m_oDocument
            
            .async = False
            .preserveWhiteSpace = False
            .resolveExternals = False
            
            ' This creates the "<?xml...> line.
            
            .appendChild .createProcessingInstruction(xtoDeclarationTarget, xtoDeclarationData)
            
            ' This creates the "XmlToObj" element.
            
            Set .documentElement = .CreateElement(xtoRoot)
            
            ' This sets the namespace to use Microsoft's data types.
            
            SetElementAttr .documentElement, xtoNameSpaceName, xtoNameSpaceValue
            
            ' This sets the whitespace handling.
            
            SetElementAttr .documentElement, xtoWhiteSpaceName, xtoWhiteSpaceValue
            
        End With
        
    Exit Sub
    
DocumentError:
    
    Set m_oDocument = Nothing
    
    Err.Raise xtoErrNoDocument
    
WriteError:
    
    Set m_oDocument = Nothing
    
    Err.Raise xtoErrWrite

End Sub

Private Function MakeElementForObject(sName As String, vObject As Variant) As MSXML.IXMLDOMElement

    ' This routine is called by WriteObject to create an element with correct attributes.
    
    On Error GoTo ErrorHandler
        
        Dim sClass As String
        
        Dim sKey As String
        
        ' This checks the object's IXmlPersistable.Class property.
        
        sClass = GetPersistableClass(vObject)
        
        ' This checks the object's IXmlCollectable.Key property. Note that a key is written even it
        ' this object is not a member of a collection. If the object does not implement IXmlCollectable,
        ' sKey will be set to "".
        
        sKey = GetCollectableKey(vObject)
        
        Set MakeElementForObject = MakeElement(sName, sClass, sKey)
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

Private Function MakeElement(sName As String, Optional vClass As Variant, Optional vKey As Variant) As MSXML.IXMLDOMElement

    ' This makes an XML element with the specified class and key attributes.
    
    On Error GoTo ErrorHandler
        
        Err.Try
            
            Dim oElement As MSXML.IXMLDOMElement
            
            ' This creates an XML element object, with its parent being the last element on the
            ' element stack. If we have just started writing, the last element on the element
            ' stack will be the "XmlToObj" element.
            
            Set oElement = CreateElement(m_oElementStack.Last, sName)
            
            If Err.Success Then
                
                SetElementAttr oElement, xtoClass, vClass
                
            End If
            
            If Err.Success Then
                
                SetElementAttr oElement, xtoKey, vKey
                
            End If
            
            If Err.Failure Then
                
                ' If we have a failure somwhere along the line, even setting one if the attributes,
                ' we want to remove the element from the xml document. For example, it could not be read
                ' back in properly if the "Class" attribute had not been written properly.
                ' Err.Finally will rereaise the appropriate error for us.
                
                DeleteElement oElement
                
                Set oElement = Nothing
                
            End If
            
            Set MakeElement = oElement
            
        Err.Finally
        
    Exit Function
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Function

Private Sub SetElementData(oElement As MSXML.IXMLDOMElement, sDataType As String, vValue As Variant)

    ' Called by WriteSimpleType to set the data type and value. We make this a separate routine
    ' so that we can easily raise an xtoErrWrite error if we have any problems with this.
    
    On Error GoTo ErrorHandler
        
        With oElement
            
            .dataType = sDataType
            
            .nodeTypedValue = vValue
            
        End With
        
    Exit Sub
    
ErrorHandler:
    
    Err.Raise xtoErrWrite

End Sub

'********************************************************************************
'**                         Private Initialize Methods                         **
'********************************************************************************

Private Function InitObject(sClass As String, Optional ByRef vArgument As Variant) As Object

    ' Called by InitProperty.
    
    On Error GoTo ErrorHandler
        
        Err.Try
            
            Dim oObject As Object
            
            ' This tries to create an object with the desired class and makes sure that it support
            ' the IXmlCreatable interface.
            
            Set oObject = MakeObject(sClass, xtoObjectCreatable)
            
            If Err.Success Then
                
                ' If this object is a parent object, then we push it onto the parent stack so that
                ' any of its children can get a reference to it. See MakeObject() and SetObjectParent().
                
                If IsParent(oObject) Then m_oParentStack.Push oObject
                
                If Err.Success Then
                    
                    ' This will call the object's InitProperties method.
                    
                    CallInitProperties CastToCreatable(oObject), vArgument
                    
                    If IsParent(oObject) Then m_oParentStack.Pop
                    
                End If
                
            End If
            
            If Err.Failure Then
                
                ' If we get any error along the line, we need to cleanly destory to object by calling
                ' its ClearProperties method. ClearObject does this for us.
                
                ClearObject oObject
                
                Set oObject = Nothing
                
            End If
            
            Set InitObject = oObject
            
        Err.Finally
        
    Exit Function
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Function

Private Sub PrepForIniting()

    ' This makes sure that the client is not already using this object to read, write or clear.
    ' If so, it raises an error. It also clears out any content that might be left over from
    ' a previous read or write operation. It then sets us up to start Initing.
    
    On Error GoTo ErrorHandler
        
        If (m_eState = xtoReading) Or (m_eState = xtoWriting) Or (m_eState = xtoClearing) Then
            
            Err.Raise xtoErrCannotInit
            
        End If
        
        If (m_eState = xtoReadyToRead) Or (m_eState = xtoHaveWritten) Or (m_eState = xtoHaveCleared) Then
            
            Call StopCurrentState
            
        End If
        
        If Not ((m_eState = xtoIniting) Or (m_eState = xtoHaveInited)) Then
            
            Call StartIniting
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub StartIniting()

    On Error GoTo ErrorHandler
        
        ' We don't need to worry about any of the XML document stuff because we don't use it for initing.
        
        Set m_oParentStack = New Stack
        
        If Not m_oParent Is Nothing Then
            
            m_oParentStack.Push m_oParent
            
        End If
        
        m_eState = xtoHaveInited
        
    Exit Sub
    
ErrorHandler:
    
    Err.Save
    
    Call StopCurrentState
    
    Err.Handle Saved:=True

End Sub

'********************************************************************************
'**                           Private Clear Methods                            **
'********************************************************************************

Private Sub ClearVariant(vObject As Variant)

    ' Called by ClearProperty.
    
    On Error GoTo ErrorHandler
        
        ' We really only have to worry about anything if its an object and it is not Nothing.
        
        If IsObject(vObject) Then
            
            If Not vObject Is Nothing Then
                
                If TypeName(vObject) = xtoCollectionClass Then
                    
                    ClearCollection vObject
                    
                Else
                    
                    ClearObject vObject
                    
                End If
                
            End If
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub ClearCollection(vCollection As Variant)

    ' The client passed in a collection, so we have to Clear all of the item in the collection.
    ' We go through all of the items in the collection and remove them and call ClearVariant on them.
    
    On Error GoTo ErrorHandler
        
        Dim oItem As Object
        
        With CastToCollection(vCollection)
            
            Do While .Count > 0
                
                If IsObject(.Item(1)) Then
                    
                    Set oItem = .Item(1)
                    
                    .Remove 1
                    
                    ClearVariant oItem
                    
                    Set oItem = Nothing
                    
                Else
                    
                    .Remove 1
                    
                End If
                
            Loop
            
        End With
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub ClearObject(vObject As Variant)

    On Error GoTo ErrorHandler
        
        Err.Try
            
            ' ClearObject is called by ReadObject and InitObject if an error is encountered. So that
            ' clearing of child objects can work properly, we have to set the state to xtoClearing,
            ' but we have to remember what state we were in when called.
            
            Dim ePrevState As xtoState
            
            ePrevState = m_eState
            
            m_eState = xtoClearing
            
            If IsCreatable(vObject) Then
                
                ' This calls the actual ClearProperties method.
                
                CallClearProperties CastToCreatable(vObject)
                
            End If
            
            ' This clear's the object's parent and raised the Delete event.
            
            KillObject CastToObject(vObject)
            
            m_eState = ePrevState
            
        Err.Finally
        
    Exit Sub
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Sub

Private Sub PrepForClearing()

    ' This makes sure that the client is not already using this object to read, write or init.
    ' If so, it raises an error. It also clears out any content that might be left over from
    ' a previous read or write operation. It then sets us up to start clearing.
    
    On Error GoTo ErrorHandler
        
        If (m_eState = xtoReading) Or (m_eState = xtoWriting) Or (m_eState = xtoIniting) Then
            
            Err.Raise xtoErrCannotClear
            
        End If
        
        If (m_eState = xtoReadyToRead) Or (m_eState = xtoHaveWritten) Or (m_eState = xtoHaveInited) Then
            
            Call StopCurrentState
            
        End If
        
        If Not ((m_eState = xtoClearing) Or (m_eState = xtoHaveCleared)) Then
            
            Call StartClearing
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub StartClearing()

    On Error GoTo ErrorHandler
        
        m_lDepth = 0
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

'********************************************************************************
'**                           Other Private Methods                            **
'********************************************************************************

Private Sub StopCurrentState()

    ' This is called when switching from one activity to another. It does a nice cleanup.
    
    On Error GoTo ErrorHandler
        
        Set m_oParentStack = Nothing
        
        Set m_oElementStack = Nothing
        
        Set m_oDocument = Nothing
        
        m_lDepth = 0
        
        m_eState = xtoNone
        
    Exit Sub
    
ErrorHandler:
    
    Resume Next

End Sub

'********************************************************************************
'**         Private Routines used to Save and Restore the XML Document         **
'********************************************************************************

Private Sub SaveXmlDocument(sFileSpec As String)

    ' We put this in its own routine so we can easily raise an appropriate error for this action.
    
    On Error GoTo ErrorHandler
        
        m_oDocument.Save sFileSpec
        
    Exit Sub
    
ErrorHandler:
    
    Err.Raise xtoErrSave

End Sub

Private Sub LoadXmlDocument(sFileSpec As String)

    ' We put this in its own routine so we can easily raise an appropriate error for this action.
    
    On Error GoTo ErrorHandler
        
        If m_oDocument.Load(sFileSpec) = False Then
            
            Err.Raise xtoErrParse
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Raise xtoErrParse

End Sub

Private Function GetDocumentXml() As String

    ' We put this in its own routine so we can easily raise an appropriate error for this action.
    
    On Error GoTo ErrorHandler
        
        GetDocumentXml = m_oDocument.xml
        
    Exit Function
    
ErrorHandler:
    
    Err.Raise xtoErrGetContents

End Function

Private Sub SetDocumentXml(sXml As String)

    ' We put this in its own routine so we can easily raise an appropriate error for this action.
    
    On Error GoTo ErrorHandler
        
        If m_oDocument.loadXML(sXml) = False Then
            
            Err.Raise xtoErrParse
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Raise xtoErrParse

End Sub

'********************************************************************************
'**               Private Routines for Working with XML Elements               **
'********************************************************************************

Private Function CreateElement(oParent As MSXML.IXMLDOMElement, sName As String) As MSXML.IXMLDOMElement

    ' Creates a child element for this given parent.
    
    On Error GoTo ErrorHandler
        
        Dim oElement As MSXML.IXMLDOMElement
        
        With oParent
            
            Set oElement = .ownerDocument.CreateElement(sName)
            
            .appendChild oElement
            
        End With
        
        Set CreateElement = oElement
        
    Exit Function
    
ErrorHandler:
    
    Set oElement = Nothing
    
    Err.Raise xtoErrWrite

End Function

Private Sub DeleteElement(oElement As MSXML.IXMLDOMElement)

    ' Removes an element from the XML document, but only if it has been actually added into the document.
    
    On Error GoTo ErrorHandler
        
        If Not oElement Is Nothing Then
            
            If Not oElement.parentNode Is Nothing Then
                
                oElement.parentNode.removeChild oElement
                
            End If
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Exit Sub

End Sub

Private Function GetChildElement(oParent As MSXML.IXMLDOMElement, sChild As String) As MSXML.IXMLDOMElement

    ' This routine searches through an Elements nodes for an element with a given name.
    ' If sChild is "", then the first element found is returned.
    
    On Error GoTo ErrorHandler
        
        Dim oChild As MSXML.IXMLDOMNode
        
        For Each oChild In oParent.childNodes
            
            If oChild.nodeType = NODE_ELEMENT Then
                
                If (sChild = "") Or (oChild.nodeName = sChild) Then
                    
                    Set GetChildElement = oChild
                    
                    Exit Function
                    
                End If
                
            End If
            
        Next
        
        Set GetChildElement = Nothing
        
    Exit Function
    
ErrorHandler:
    
    Set GetChildElement = Nothing

End Function

Private Sub SetElementAttr(oElement As MSXML.IXMLDOMElement, sName As String, Optional vValue As Variant)

    ' Sets an attribute for an element, but only if it is not an empty string.
    
    On Error GoTo ErrorHandler
        
        If Not IsMissing(vValue) Then
            
            If CStr(vValue) <> "" Then
                
                oElement.setAttribute sName, vValue
                
            End If
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Raise xtoErrWrite

End Sub

Private Function GetElementAttr(oElement As MSXML.IXMLDOMElement, sName As String) As String

    ' Retreives the value of an attribute for an element. Returns "" if the attribute does not exist.
    
    On Error GoTo ErrorHandler
        
        Dim vValue As Variant
        
        vValue = oElement.getAttribute(sName)
        
        If IsNull(vValue) Then
            
            GetElementAttr = ""
            
        Else
            
            GetElementAttr = CStr(vValue)
            
        End If
        
    Exit Function
    
ErrorHandler:
    
    Err.Raise xtoErrRead

End Function

Private Function NodeToElement(oNode As MSXML.IXMLDOMNode) As MSXML.IXMLDOMElement

    ' This routine is used to make sure an XML node is really and element.
    
    On Error GoTo ErrorHandler
        
        If oNode.nodeType = NODE_ELEMENT Then
            
            Set NodeToElement = oNode
            
        Else
            
            Set NodeToElement = Nothing
            
        End If
        
    Exit Function
    
ErrorHandler:
    
    Err.Raise xtoErrRead

End Function

'********************************************************************************
'**               Private Object Creation and Deletion Routines                **
'********************************************************************************

Private Function MakeObject(sClass As String, eType As xtoObjectType) As Object

    ' This routine creates and initializes an object.
    
    On Error GoTo ErrorHandler
        
        Err.Try
            
            Dim oObject As Object
            
            ' This creates the actual object, using the Create event.
            
            Set oObject = CreateClassObject(sClass)
            
            If Err.Success Then
                
                ' This sets the object's parent using IXmlChild.
                
                SetObjectParent oObject
                
            End If
            
            If Err.Success Then
                
                ' This makes sure the object supports the requestes interface, raising
                ' an error if it does not.
                
                EnsureObjectType oObject, eType
                
            End If
            
            If Err.Failure Then
                
                ' If we get an error anywhere along the line, we need to kill the object
                
                KillObject oObject
                
                Set oObject = Nothing
                
            End If
            
            Set MakeObject = oObject
            
        Err.Finally
        
    Exit Function
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Function

Private Sub KillObject(oObject As Object)

    On Error GoTo ErrorHandler
        
        Err.Try
            
            If Not oObject Is Nothing Then
                
                ' This sets the object's IXmlChild Parent property to nothing.
                
                ClearObjectParent oObject
                
                RaiseDeleteEvent oObject
                
            End If
            
        Err.Finally
        
    Exit Sub
    
ErrorHandler:
    
    Err.Catch
    
    Resume Next

End Sub

Private Function CreateClassObject(sClass As String) As Object

    On Error GoTo ErrorHandler
        
        Dim oObject As Object
        
        Set oObject = Nothing
        
        ' First we raise the Create event allowing the client to create the object.
        
        RaiseCreateEvent sClass, oObject
        
        If oObject Is Nothing Then
            
            ' Then we try to creat the object ourselves.
            
            Set oObject = SafeCreateObject(sClass)
            
            If oObject Is Nothing Then
                
                Err.Raise xtoErrCreateObject
                
            End If
            
        End If
        
        Set CreateClassObject = oObject
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

Private Sub EnsureObjectType(vObject As Object, eType As xtoObjectType)

    ' This checks to make sure the object supports the given interface.
    
    On Error GoTo ErrorHandler
        
        Select Case eType
            
            Case xtoObjectCreatable
                
                If Not IsCreatable(vObject) Then
                    
                    Err.Raise xtoErrNotCreatable
                    
                End If
                
            Case xtoObjectPersistable
                
                If Not IsPersistable(vObject) Then
                    
                    Err.Raise xtoErrNotPersistable
                    
                End If
                
        End Select
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub SetObjectParent(oObject As Object)

    ' If the object supports the IXmlChild interface and we have a parent on the parent stack, then
    ' we set the object's parent.
    
    On Error GoTo ErrorHandler
        
        Dim oChild As IXmlChild
        
        If (Not m_oParentStack.IsEmpty) And (IsChild(oObject)) Then
            
            Set oChild = oObject
            
            CallSetParent oChild, m_oParentStack.Last
            
            Set oChild = Nothing
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub ClearObjectParent(oObject As Object)

    ' If the object supports the IXmlChild interface then we clear the object's parent.
    
    On Error GoTo ErrorHandler
        
        Dim oChild As IXmlChild
        
        If IsChild(oObject) Then
            
            Set oChild = oObject
            
            CallSetParent oChild, Nothing
            
            Set oChild = Nothing
            
        End If
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

'********************************************************************************
'**            Private Routines used to Manage the State and Depth             **
'********************************************************************************

Private Sub IncreaseState(eEnterState As xtoState)

    On Error GoTo ErrorHandler
        
        m_eState = eEnterState
        
        m_lDepth = m_lDepth + 1
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle

End Sub

Private Sub DecreaseState(eExitState As xtoState)

    On Error GoTo ErrorHandler
        
        m_lDepth = m_lDepth - 1
        
        If m_lDepth = 0 Then m_eState = eExitState
        
    Exit Sub
    
ErrorHandler:
    
    Resume Next

End Sub

'********************************************************************************
'**     Private Routines used to Raise Events and Call External Interfaces     **
'********************************************************************************
'
' These are placed in separate routines so we can call Err.Handle with
' External set to True. This allows us to catch errors that were not
' handled by the client's event procedure or interface method.
'
'********************************************************************************
Private Sub RaiseCreateEvent(ByVal sClass As String, ByRef oObject As Object)

    On Error GoTo ErrorHandler
        
        RaiseEvent CreateObject(sClass, oObject)
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle External:=True

End Sub

Private Sub RaiseDeleteEvent(ByVal oObject As Object)

    On Error GoTo ErrorHandler
        
        RaiseEvent DeleteObject(oObject)
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle External:=True

End Sub

Private Sub CallWriteProperties(oPersistable As IXmlPersistable)

    On Error GoTo ErrorHandler
        
        oPersistable.WriteProperties Me
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle External:=True

End Sub

Private Sub CallReadProperties(oPersistable As IXmlPersistable, Optional ByRef vArgument As Variant)

    On Error GoTo ErrorHandler
        
        oPersistable.ReadProperties Me, vArgument
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle External:=True

End Sub

Private Sub CallInitProperties(oCreatable As IXmlCreatable, Optional ByRef vArgument As Variant)

    On Error GoTo ErrorHandler
        
        oCreatable.InitProperties Me, vArgument
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle External:=True

End Sub

Private Sub CallClearProperties(oCreatable As IXmlCreatable)

    On Error GoTo ErrorHandler
        
        oCreatable.ClearProperties Me
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle External:=True

End Sub

Private Sub CallSetParent(oChild As IXmlChild, oParent As Object)

    On Error GoTo ErrorHandler
        
        Set oChild.Parent = oParent
        
    Exit Sub
    
ErrorHandler:
    
    Err.Handle External:=True

End Sub

Private Function CallGetClass(oPersistable As IXmlPersistable) As String

    On Error GoTo ErrorHandler
        
        CallGetClass = oPersistable.Class
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle External:=True

End Function

Private Function CallGetKey(oCollectable As IXmlCollectable) As String

    On Error GoTo ErrorHandler
        
        CallGetKey = oCollectable.Key
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle External:=True

End Function

'********************************************************************************
'*                        Private Type Checking Routines                       **
'********************************************************************************

Private Function IsParent(oObject As Object) As Boolean

    On Error Resume Next
    
    Dim oParent As IXmlParent
    
    Set oParent = oObject
    
    If Not oParent Is Nothing Then
        
        IsParent = True
        
    Else
        
        IsParent = False
        
    End If

End Function

Private Function IsChild(oObject As Object) As Boolean

    On Error Resume Next
    
    Dim oChild As IXmlChild
    
    Set oChild = oObject
    
    If Not oChild Is Nothing Then
        
        IsChild = True
        
    Else
        
        IsChild = False
        
    End If

End Function

Private Function IsPersistable(vObject As Variant) As Boolean

    On Error Resume Next
    
    Dim oPersistable As IXmlPersistable
    
    Set oPersistable = vObject
    
    If Not oPersistable Is Nothing Then
        
        IsPersistable = True
        
    Else
        
        IsPersistable = False
        
    End If

End Function

Private Function IsCreatable(vObject As Variant) As Boolean

    On Error Resume Next
    
    Dim oCreatable As IXmlCreatable
    
    Set oCreatable = vObject
    
    If Not oCreatable Is Nothing Then
        
        IsCreatable = True
        
    Else
        
        IsCreatable = False
        
    End If

End Function

Private Function IsCollectable(vObject As Variant) As Boolean

    On Error Resume Next
    
    Dim oCollectable As IXmlCollectable
    
    Set oCollectable = vObject
    
    If Not oCollectable Is Nothing Then
        
        IsCollectable = True
        
    Else
        
        IsCollectable = False
        
    End If

End Function

Private Function GetPersistableClass(vObject As Variant) As String

    On Error GoTo ErrorHandler
        
        If IsPersistable(vObject) Then
            
            GetPersistableClass = CallGetClass(CastToPersistable(vObject))
            
        Else
            
            Err.Raise xtoErrNotPersistable
            
        End If
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

Private Function GetCollectableKey(vObject As Variant) As String

    On Error GoTo ErrorHandler
        
        If IsCollectable(vObject) Then
            
            GetCollectableKey = CallGetKey(CastToCollectable(vObject))
            
        Else
            
            GetCollectableKey = ""
            
        End If
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

'********************************************************************************
'**                           Private Casting Routines                         **
'********************************************************************************

Private Function CastToObject(vObject As Variant) As Object

    On Error GoTo ErrorHandler
        
        Set CastToObject = vObject
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

Private Function CastToCollection(vObject As Variant) As Collection

    On Error GoTo ErrorHandler
        
        Set CastToCollection = vObject
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

Private Function CastToPersistable(vObject As Variant) As IXmlPersistable

    On Error GoTo ErrorHandler
        
        Set CastToPersistable = vObject
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

Private Function CastToCreatable(vObject As Variant) As IXmlCreatable

    On Error GoTo ErrorHandler
        
        Set CastToCreatable = vObject
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

Private Function CastToCollectable(vObject As Variant) As IXmlCollectable

    On Error GoTo ErrorHandler
        
        Set CastToCollectable = vObject
        
    Exit Function
    
ErrorHandler:
    
    Err.Handle

End Function

'********************************************************************************
'**                           Class Event Procedures                           **
'********************************************************************************

Private Sub Class_Initialize()

    On Error GoTo ErrorHandler
        
        Set Err = New ErrorHandler
        
        Call StopCurrentState
        
    Exit Sub
    
ErrorHandler:
    
    VBA.Err.Raise xtoErrInitialize, "XmlToObj.XmlPropertyBag", "xtoErrInitialize - An error occured while initializing the property bag."

End Sub

Private Sub Class_Terminate()

    ' We want to preserve any error information that might be active while we are terminating.
    
    Dim lErrNumber As Long
    Dim sErrSource As String
    Dim sErrDescription As String
    
    lErrNumber = VBA.Err.Number
    sErrSource = VBA.Err.Source
    sErrDescription = VBA.Err.Description
    
    On Error GoTo ErrorHandler
        
        Call StopCurrentState
        
        Set Err = Nothing
        
ExitHandler:
        
        VBA.Err.Number = lErrNumber
        VBA.Err.Source = sErrSource
        VBA.Err.Description = sErrDescription
        
    Exit Sub
    
ErrorHandler:
    
    Resume Next

End Sub

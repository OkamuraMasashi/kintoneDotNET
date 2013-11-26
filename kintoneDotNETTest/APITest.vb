﻿Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports System.Configuration
Imports kintoneDotNET.API
Imports kintoneDotNET.API.Types
Imports System.IO

''' <summary>
''' kintoneAPIの単体テストを実施する
''' </summary>
''' <remarks></remarks>
<TestClass()>
Public Class APITest
    Inherits AbskintoneTest

    ''' <summary>
    ''' Readのテスト(単純に読み込みで例外が発生しないことを確認)
    ''' </summary>
    ''' <remarks></remarks>
    <TestMethod()>
    Public Sub Read()

        Dim result As List(Of kintoneTestModel) = kintoneTestModel.Find(Of kintoneTestModel)(String.Empty)

        For Each item As kintoneTestModel In result
            Console.WriteLine(item)
        Next

    End Sub

    <TestMethod()>
    Public Sub ReadExpression()
        Dim querys As New Dictionary(Of String, String)
        Dim q As String = ""
        'Simple Expression
        q = kintoneQuery.Make(Of kintoneTestModel)(Function(x) x.status > "a", AbskintoneModel.GetPropertyToDefaultDic)
        Console.WriteLine("querySimple1:" + q)
        Assert.AreEqual("ステータス > ""a""", q)

        q = kintoneQuery.Make(Of kintoneTestModel)(Function(x) Not x.status > "a", AbskintoneModel.GetPropertyToDefaultDic)
        Console.WriteLine("querySimple2:" + q)
        Assert.AreEqual("ステータス <= ""a""", q)

        'Equal
        q = kintoneQuery.Make(Of kintoneTestModel)(Function(x) x.status.Equals(1) Or x.record_id <> 1 And Not x.numberField = 1)
        Console.WriteLine("queryEqual:" + q)
        Assert.AreEqual("status = 1 or record_id != 1 and numberField != 1", q)

        'like
        q = kintoneQuery.Make(Of kintoneTestModel)(Function(x) x.status Like "A" And Not x.link Like "http")
        Console.WriteLine("queryLike:" + q)
        Assert.AreEqual("status like ""A"" and link not like ""http""", q)

        'IN
        q = kintoneQuery.Make(Of kintoneTestModel)(Function(x) {"1", "a"}.Contains(x.radio) And Not New List(Of String)() From {"c", "d"}.Contains(x.radio))
        Console.WriteLine("queryArray&List:" + q)
        Assert.AreEqual("radio in (""1"",""a"") and radio not in (""c"",""d"")", q)

        'method Equal
        q = kintoneQuery.Make(Of kintoneTestModel)(Function(x) x.status = String.Empty And x.created_time < DateTime.MaxValue)
        Console.WriteLine("queryMethodCall:" + q)
        Assert.AreEqual("status = """" and created_time < ""9999-12-31T23:59:59+09:00""", q)


    End Sub


    ''' <summary>
    ''' FindAllのテスト 全件取得可能かチェックする
    ''' テストアプリには制限を超えるようなレコード数は登録しないため、Limitを調整しテスト
    ''' </summary>
    ''' <remarks></remarks>
    <TestMethod()>
    Public Sub FindAllTest()

        Dim result As List(Of kintoneTestModel) = kintoneTestModel.Find(Of kintoneTestModel)(String.Empty)

        Dim before As Integer = kintoneAPI.ReadLimit
        kintoneAPI.ReadLimit = 1
        Dim resultAll As List(Of kintoneTestModel) = kintoneTestModel.FindAll(Of kintoneTestModel)(String.Empty)

        Assert.AreEqual(result.Count, resultAll.Count)
        For Each item In result
            Dim sameItem As kintoneTestModel = resultAll.Find(Function(x) x.record_id = item.record_id)
            Assert.IsTrue(sameItem IsNot Nothing)
        Next

        kintoneAPI.ReadLimit = before

    End Sub


    ''' <summary>
    ''' レコードの登録/更新/削除
    ''' ※複数のTestMethodに分かれて行うとidが消えたり削除されたりして予期せずエラーになる場合があるので、一つにまとめる
    ''' </summary>
    ''' <remarks></remarks>
    <TestMethod()>
    Public Sub ExecuteCreateDelete()
        Const METHOD_NAME As String = "ExecuteCreateDelete"

        '事前に削除
        Dim remained As List(Of kintoneTestModel) = kintoneTestModel.Find(Of kintoneTestModel)(Function(x) x.methodinfo = METHOD_NAME).OrderBy(Function(x) x.textarea).ToList
        kintoneTestModel.Delete(Of kintoneTestModel)(remained.Select(Function(x) x.record_id).ToList)

        '単一のケース -------------------------------------------------------------
        Dim item As New kintoneTestModel
        item.methodinfo = METHOD_NAME

        Dim id As String = item.Create
        Assert.IsFalse(String.IsNullOrEmpty(id))
        item.record_id = id

        Assert.IsTrue(item.Delete())
        Dim result As List(Of kintoneTestModel) = kintoneTestModel.Find(Of kintoneTestModel)("methodinfo=""" + METHOD_NAME + """")


        '複合のケース -------------------------------------------------------------
        Dim list As New List(Of kintoneTestModel)

        Dim before As Integer = kintoneAPI.ExecuteLimit
        kintoneAPI.ExecuteLimit = 1

        For i As Integer = 0 To 2
            Dim m As New kintoneTestModel
            m.methodinfo = METHOD_NAME
            m.textarea = "bulk insert " + (i + 1).ToString
            list.Add(m)
        Next

        '登録
        Dim ids As List(Of kintoneTestModel) = kintoneTestModel.Create(list)
        Assert.AreEqual(list.Count, ids.Count)
        Dim inserted As List(Of kintoneTestModel) = kintoneTestModel.Find(Of kintoneTestModel)(Function(x) x.methodinfo = METHOD_NAME).OrderBy(Function(x) x.textarea).ToList

        For i As Integer = 0 To 2
            Assert.AreEqual(list(i).textarea, inserted(i).textarea)
            inserted(i).textarea = "bulk updated " + (i + 1).ToString
        Next

        '更新
        Assert.IsTrue(kintoneTestModel.Update(inserted))
        Dim updated As List(Of kintoneTestModel) = kintoneTestModel.Find(Of kintoneTestModel)(Function(x) x.methodinfo = METHOD_NAME).OrderBy(Function(x) x.textarea).ToList
        For i As Integer = 0 To 2
            Assert.AreEqual(inserted(i).textarea, updated(i).textarea)
        Next

        '削除
        Assert.IsTrue(kintoneTestModel.Delete(Of kintoneTestModel)(updated.Select(Function(x) x.record_id).ToList))

        kintoneAPI.ExecuteLimit = before

    End Sub

    ''' <summary>
    ''' 文字列フィールドのRead/Writeテスト
    ''' </summary>
    ''' <remarks></remarks>
    <TestMethod()>
    Public Sub ExecuteUpdateReadString()
        Const updString As String = "文字列"
        Const updTexts As String = "テスト実行" + vbCrLf + "テスト実行"

        Dim item As kintoneTestModel = getInitializedRecord()
        Assert.IsFalse(String.IsNullOrEmpty(item.record_id))

        item.stringField = updString
        item.textarea = updTexts

        Assert.IsTrue(item.Update())

        item = getRecordForUpdateAndRead()
        Assert.IsTrue(item.stringField = updString)
        Assert.IsTrue(item.textarea = updTexts)

    End Sub

    ''' <summary>
    ''' 日付/時刻/日付時刻型フィールドのRead/Writeテスト
    ''' </summary>
    ''' <remarks></remarks>
    <TestMethod()>
    Public Sub ExecuteUpdateReadDate()

        Dim updDate As New DateTime(1000, 2, 3)
        Dim updTime As New DateTime(1000, 1, 2, 11, 22, 33)
        Dim updDateTime As New DateTime(9999, 12, 31, 23, 53, 59)

        Dim item As kintoneTestModel = getInitializedRecord()
        Assert.IsFalse(String.IsNullOrEmpty(item.record_id))

        item.dateField = updDate
        item.time = updTime
        item.datetimeField = updDateTime

        Assert.IsTrue(item.Update())

        item = getRecordForUpdateAndRead()
        Assert.AreEqual(updDate.ToString("yyyyMMdd"), item.dateField.ToString("yyyyMMdd"))
        'TODO 要確認：更新時 秒の情報が落ちる？(秒の値が常に00になっている)
        Assert.AreEqual(updTime.ToString("HHmm"), item.time.ToString("HHmm"))
        Assert.AreEqual(updDateTime.ToString("yyyyMMdd HHmm"), item.datetimeField.ToString("yyyyMMdd HHmm"))


    End Sub

    ''' <summary>
    ''' 複数選択項目のRead/Writeテスト
    ''' </summary>
    ''' <remarks></remarks>
    <TestMethod()>
    Public Sub ExecuteUpdateReadMultiSelect()
        Dim updCheck As New List(Of String) From {"check2"}
        Dim updSelect As New List(Of String) From {"select1", "select3"}

        Dim item As kintoneTestModel = getInitializedRecord()
        Assert.IsFalse(String.IsNullOrEmpty(item.record_id))

        item.checkbox = updCheck
        item.multiselect = updSelect

        Assert.IsTrue(item.Update())

        item = getRecordForUpdateAndRead()
        Assert.IsTrue(ListEqual(Of String)(updCheck, item.checkbox))
        Assert.IsTrue(ListEqual(Of String)(updSelect, item.multiselect))

    End Sub

    ''' <summary>
    ''' kintoneへファイルをアップロード/ダウンロードする
    ''' </summary>
    ''' <remarks></remarks>
    <TestMethod()>
    Public Sub UploadDownloadFile()
        Const DOWNLOAD_FILE As String = "downloadedFile.PNG"
        Dim item As kintoneTestModel = getInitializedRecord()

        'ダウンロードファイルを事前に削除
        System.IO.File.Delete(FileOutPath(DOWNLOAD_FILE))

        'ファイルをアップロード
        Dim file As PostedFile = New PostedFile(FileOutPath("_uploadFile.PNG"))
        item.attachfile.UploadFile(file)
        Assert.IsTrue(item.attachfile.Count > 0)
        item.Update()

        'ファイルをダウンロードし、書き出し
        item = getRecordForUpdateAndRead()
        Dim downloaded As MemoryStream = item.attachfile(0).GetFile()
        Using target As New FileStream(FileOutPath(DOWNLOAD_FILE), FileMode.Create)
            target.Write(downloaded.ToArray, 0, downloaded.ToArray.Length)
        End Using

        Assert.IsTrue(System.IO.File.Exists(FileOutPath(DOWNLOAD_FILE)))

    End Sub

    ''' <summary>
    ''' 内部テーブルのRead/Writeテスト
    ''' </summary>
    ''' <remarks></remarks>
    <TestMethod()>
    Public Sub UploadDownloadSubTable()

        Dim addLog As New ChangeLog(New DateTime(1000, 1, 1), "The Beggining Day ")
        Dim nextLog As New ChangeLog(DateTime.MaxValue, "The End of Century")
        Dim updLog As New ChangeLog(New DateTime(1999, 7, 31), "The Ending Day")

        '現在のレコードを取得
        Dim item As kintoneTestModel = getInitializedRecord()
        Assert.IsTrue(item.changeLogs.Count = 0)

        '内部テーブルレコードを追加
        item.changeLogs.Add(addLog)
        Assert.IsTrue(item.Update())

        item = getRecordForUpdateAndRead()
        Assert.AreEqual(addLog.changeYMD.ToString("yyyyMMdd"), item.changeLogs(0).changeYMD.ToString("yyyyMMdd"))
        Assert.AreEqual(addLog.historyDesc, item.changeLogs(0).historyDesc)

        'もう一件追加+内容を編集
        updLog.id = item.changeLogs(0).id
        item.changeLogs(0) = updLog
        item.changeLogs.Add(nextLog)

        Assert.IsTrue(item.Update())

        item = getRecordForUpdateAndRead()
        Assert.AreEqual(item.changeLogs.Count, 2)

        Dim log As ChangeLog = item.changeLogs.Find(Function(x) x.id = updLog.id)
        Assert.AreEqual(updLog.changeYMD.ToString("yyyyMMdd"), log.changeYMD.ToString("yyyyMMdd"))
        Assert.AreEqual(updLog.historyDesc, log.historyDesc)

    End Sub

    ''' <summary>
    ''' テストファイル保管用フォルダへのパスを取得する
    ''' </summary>
    ''' <param name="fileName"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function FileOutPath(ByVal fileName As String)
        '{ProjectRoot}/bin/Debug(orRelease)/xxx.dll で実行されるため、Parent/Parentでルートに戻る
        Return New DirectoryInfo(Environment.CurrentDirectory).Parent.Parent.FullName.ToString + "/App_Data/" + fileName
    End Function


End Class
﻿Imports System.Reflection
Imports kintoneDotNET.API.Types
Imports System.Linq.Expressions

Namespace API

    ''' <summary>
    ''' kintoneのレコードに対応するモデルの基となる、抽象クラス
    ''' </summary>
    ''' <remarks>
    ''' 読込は全てのプロパティに対して行われるが、kintoneへ送信するのはkintoneItemAttribute属性が付与されており、isUpload=Trueのもの(デフォルトTrue)<br/>
    ''' このため、kintone側で更新したいプロパティについては&lt;kintoneItem()&gt;を付与する。<br/>
    ''' リスト型のデータ(チェックボックスリストや添付ファイルなど)については、List(Of )で宣言を行う必要あり
    ''' </remarks>
    Public MustInherit Class AbskintoneModel

        ''' <summary>
        ''' kintoneのアプリケーションID
        ''' </summary>
        Public MustOverride ReadOnly Property app As String

        ''' <summary>
        ''' [共通]レコード番号
        ''' </summary>
        Public Overridable Property record_id As String = ""

        ''' <summary>
        ''' [共通]登録時刻
        ''' </summary>
        <kintoneItem(FieldType:=kintoneDatetime.DateTimeType, isUpload:=False)>
        Public Overridable Property created_time As DateTime = DateTime.MinValue

        ''' <summary>
        ''' [共通]更新時刻
        ''' </summary>
        <kintoneItem(FieldType:=kintoneDatetime.DateTimeType, isUpload:=False)>
        Public Overridable Property updated_time As DateTime = DateTime.MinValue

        ''' <summary>
        ''' [共通]登録者
        ''' </summary>
        Public Overridable Property create_usr As New kintoneUser()

        ''' <summary>
        ''' [共通]更新者
        ''' </summary>
        Public Overridable Property update_usr As New kintoneUser()

        ''' <summary>
        ''' [共通]ステータス (※プロセスを使っている場合値を取得可能)
        ''' </summary>
        Public Overridable Property status As String = ""

        ''' <summary>
        ''' [共通]作業者
        ''' </summary>
        Public Overridable Property work_usr As New kintoneUser()

        Private _convertDictionary As New Dictionary(Of String, String) From {
                                    {"レコード番号", "record_id"},
                                    {"作成日時", "created_time"},
                                    {"更新日時", "updated_time"},
                                    {"作成者", "create_usr"},
                                    {"更新者", "update_usr"},
                                    {"ステータス", "status"},
                                    {"作業者", "work_usr"}
                                }
        Protected Overridable Property ConvertDictionary As Dictionary(Of String, String)
            Get
                Return _convertDictionary
            End Get
            Set(value As Dictionary(Of String, String))
                _convertDictionary = value
            End Set
        End Property


        ''' <summary>
        ''' kintone上でのレコードURL
        ''' </summary>
        Public ReadOnly Property record_show_url() As String
            Get
                Dim api As New kintoneAPI(app)
                Dim url As String = "https://" + kintoneAPI.Host + "/k/" + app + "/show?record=" + record_id
                Return url
            End Get
        End Property

        ''' <summary>
        ''' レコードの検索を行う(expression指定)
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="expression">Booleanを返却する関数式</param>
        ''' <param name="isConvert">デフォルトの項目変換をかけるか否か</param>
        ''' <returns></returns>
        ''' <remarks>
        ''' kintone上ではレコード番号などのデフォルト項目をrecord_id等でなく「レコード番号」と日本語そのままでもっているため、この名前で検索を行わないとエラーになります<br/>
        ''' isConvert=True(デフォルト値)としておけば、record_id->レコード番号といったデフォルト項目の変換を自動で行ってくれます。<br/>
        ''' <example>
        ''' <para>
        ''' <code>
        '''   'AbskintoneModelを継承して作成したBookModelを使用し、検索を行う
        '''   Dim list AS List(Of BookModel) = BookModel.Find(Of BookModel)(Function(x) x.title Like "Mathematics" And x.price &lt; 3000 )
        '''   
        '''   'Attributeを設定しておけば、日付型の条件指定もDateTime型オブジェクトから直接行えます
        '''   Dim upds AS List(Of BookModel) = BookModel.Find(Of BookModel)(Function(x) x.updated_time >= DateTime.Now)
        ''' </code>
        ''' </para>
        ''' </example>
        ''' </remarks>
        Public Shared Function Find(Of T As AbskintoneModel)(ByVal expression As Expression(Of Func(Of T, Boolean)), _
                                                                Optional ByVal isConvert As Boolean = True) As List(Of T)
            Dim model As T = Activator.CreateInstance(Of T)()
            Dim query As String = kintoneQuery.Make(Of T)(expression, If(isConvert, model.GetPropertyToDefaultDic, Nothing))
            Return Find(Of T)(query)
        End Function

        ''' <summary>
        ''' レコードの検索を行う(文字列クエリ指定)<br/>
        ''' ※クエリを作成する際、title="hoge"と文字列型の場合""で比較値を囲う必要がある点に注意してください<br/>
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="query">任意のクエリ文字列</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function Find(Of T As AbskintoneModel)(ByVal query As String) As List(Of T)
            Return GetAPI(Of T).Find(Of T)(query)
        End Function

        ''' <summary>
        ''' id指定によるレコードの検索
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="id"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function FindById(Of T As AbskintoneModel)(ByVal id As String) As T
            Dim result As List(Of T) = Find(Of T)(Function(x) x.record_id = id)
            If result IsNot Nothing AndAlso result.Count > 0 Then
                Return result.First
            Else
                Return Nothing
            End If
        End Function

        ''' <summary>
        ''' idの複数指定によるレコードの検索
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="ids"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function FindByIds(Of T As AbskintoneModel)(ByVal ids As List(Of String)) As List(Of T)
            Dim result As List(Of T) = Nothing
            If ids Is Nothing Then Return Nothing

            If ids.Count > kintoneAPI.ReadLimit Then
                result = FindAll(Of T)(Function(x) ids.Contains(x.record_id))
            Else
                result = Find(Of T)(Function(x) ids.Contains(x.record_id))
            End If
            Return result
        End Function

        ''' <summary>
        ''' レコードの検索を行う(全件)(expression指定)<br/>
        ''' kintone APIの上限値を超える件数のレコードを取得します
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="expression"></param>
        ''' <param name="nameConvertor"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function FindAll(Of T As AbskintoneModel)(ByVal expression As Expression(Of Func(Of T, Boolean)), _
                                                                Optional ByVal nameConvertor As Dictionary(Of String, String) = Nothing) As List(Of T)
            Dim query As String = kintoneQuery.Make(Of T)(expression, nameConvertor)
            Return FindAll(Of T)(query)
        End Function

        ''' <summary>
        ''' レコードの検索を行う(全件)(文字列クエリ指定)<br/>
        ''' kintone APIの上限値を超える件数のレコードを取得します
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="query"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function FindAll(Of T As AbskintoneModel)(ByVal query As String) As List(Of T)
            Return GetAPI(Of T).FindAll(Of T)(query)
        End Function


        ''' <summary>
        ''' レコードの登録を行う<br/>
        ''' 登録を行った後、登録を行ったレコードをkintone上から取得し返却します
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="objs">登録対象オブジェクト</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function Create(Of T As AbskintoneModel)(ByVal objs As List(Of T)) As List(Of T)
            Dim ids As List(Of String) = GetAPI(Of T)().BulkCreate(Of T)(objs)
            Return FindByIds(Of T)(ids)
        End Function

        ''' <summary>
        ''' レコードの更新を行う
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="objs"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function Update(Of T As AbskintoneModel)(ByVal objs As List(Of T)) As Boolean
            Return GetAPI(Of T).BulkUpdate(Of T)(objs)
        End Function

        ''' <summary>
        ''' レコードの保存を行う<br/>
        ''' モデル上 isKey = True と設定された項目をキーとし、一致するキーがある場合はUpdate、なければCreateを行う
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="objs"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function Save(Of T As AbskintoneModel)(ByVal objs As List(Of T)) As List(Of T)
            Dim ids As List(Of String) = GetAPI(Of T)().BulkSave(Of T)(objs)
            Return FindByIds(Of T)(ids)

        End Function

        ''' <summary>
        ''' レコードの削除を行う
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="ids"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Shared Function Delete(Of T As AbskintoneModel)(ByVal ids As List(Of String)) As Boolean
            Return GetAPI(Of T).BulkDelete(Of T)(ids)
        End Function

        Public Shared Function Delete(Of T As AbskintoneModel)(ByVal objs As List(Of T)) As Boolean
            Dim model As T = Activator.CreateInstance(Of T)()
            objs = model.UpdateHook(objs) 'idをセット

            Dim result As Boolean = GetAPI(Of T).BulkDelete(Of T)((From x As T In objs Select x.record_id).ToList)

            If result Then '成功した場合、objsに設定されていたidをクリアする(削除されたため)
                objs.ForEach(Function(x) x.record_id = String.Empty)
            End If

            Return result

        End Function


        ''' <summary>
        ''' レコードの登録(単一)を行う<br/>
        ''' 自身をkintone上に登録します
        ''' </summary>
        Public Function Create() As String
            Dim result As Object = execute("Create", Me)

            If Not String.IsNullOrEmpty(result.ToString) Then
                Me.record_id = result.ToString
            End If
            Return Me.record_id
        End Function

        ''' <summary>
        ''' レコードの更新(単一)を行う<br/>
        ''' 自身のレコードを更新します
        ''' </summary>
        Public Function Update() As Boolean
            Dim result As Boolean = CBool(execute("Update", Me))
            Return result
        End Function

        ''' <summary>
        ''' レコードの保存を行う<br/>
        ''' isKey = True で設定されたレコードがある場合更新、なければ登録を行います。
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Function Save() As String
            Dim result As String = CStr(execute("Save", Me))

            If Not String.IsNullOrEmpty(result) Then
                Me.record_id = result
            End If

            Return result

        End Function

        ''' <summary>
        ''' レコードの削除(単一)を行う<br/>
        ''' 自身のIDに一致するレコードを削除します
        ''' </summary>
        Public Function Delete() As Boolean

            If String.IsNullOrEmpty(Me.record_id) Then
                'レコードidの設定がない場合、keyからidの取得を試みる
                execute("SetIdToModel", Me)
            End If

            Dim result As Boolean = CBool(execute("Delete", Me.record_id))
            If result Then '削除に成功したら、idをクリアする
                Me.record_id = String.Empty
            End If

            Return result

        End Function

        ''' <summary>
        ''' 実体である自身のタイプでkintoneAPIのジェネリクスメソッドをコールする
        ''' </summary>
        ''' <param name="methodName"></param>
        ''' <param name="params"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Function execute(ByVal methodName As String, ParamArray params As Object()) As Object
            Dim method As MethodInfo = (From m As MethodInfo In GetType(kintoneAPI).GetMethods Where m.Name = methodName).FirstOrDefault
            Dim generic As MethodInfo = method.MakeGenericMethod(Me.GetType)
            Dim result As Object = Nothing

            Try
                result = generic.Invoke(GetAPI, params)
            Catch ex As System.Reflection.TargetInvocationException
                'リフレクションによる呼び出しの場合本来の例外が内部に隠蔽されるため、取り出し
                Throw ex.InnerException
            End Try

            Return result

        End Function


        ''' <summary>
        ''' Create処理実行前に行われる処理<br/>
        ''' 事前に行っておくべき処理(値設定/対象の追加・削除)があればここに実装する
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="objs">Create対象オブジェクト</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Overridable Function CreateHook(Of T As AbskintoneModel)(ByVal objs As List(Of T)) As List(Of T)
            Return objs
        End Function

        ''' <summary>
        ''' Update処理実行前に行われる処理<br/>
        ''' 事前に行っておくべき処理(値設定/対象の追加・削除)があればここに実装する
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="objs">>Update対象オブジェクト</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Overridable Function UpdateHook(Of T As AbskintoneModel)(ByVal objs As List(Of T)) As List(Of T)

            Dim ids As List(Of String) = (From x As T In objs Select x.record_id).ToList
            Dim idline As String = String.Join("", ids)

            If String.IsNullOrEmpty(idline) Then 'レコードidの設定がない場合、keyからidの取得を試みる
                GetAPI(Of T).SetIdsToModels(Of T)(objs)
            End If

            Return objs

        End Function

        ''' <summary>
        ''' Delete処理実行前に行われる処理<br/>
        ''' 事前に行っておくべき処理(値設定/対象の追加・削除)があればここに実装する
        ''' </summary>
        ''' <typeparam name="T"></typeparam>
        ''' <param name="ids">Delete対象id</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Overridable Function DeleteHook(Of T As AbskintoneModel)(ByVal ids As List(Of String)) As List(Of String)
            Return ids
        End Function


        ''' <summary>
        ''' 自身を操作するAPIを取得する
        ''' </summary>
        Private Function GetAPI() As kintoneAPI
            Dim api As New kintoneAPI(app)
            Return api
        End Function

        ''' <summary>
        ''' 自身を操作するAPIを取得する(Shared Method用)
        ''' </summary>
        Private Shared Function GetAPI(Of T As AbskintoneModel)() As kintoneAPI
            Dim model As T = Activator.CreateInstance(Of T)()
            Dim api As New kintoneAPI(model.app)
            Return api
        End Function

        ''' <summary>
        ''' kintone上デフォルトで日本語である項目("レコード番号","作成日時" など)をプロパティ名(record_id,updated_time etc)に変換するためのDictionaryを取得する
        ''' </summary>
        Public Function GetDefaultToPropertyDic() As Dictionary(Of String, String)
            Return GetNameConvertDic(True)
        End Function

        ''' <summary>
        ''' プロパティ名をkintone上のデフォルト名称に変換するためのDictionaryを取得する
        ''' </summary>
        Public Function GetPropertyToDefaultDic() As Dictionary(Of String, String)
            Return GetNameConvertDic(False)
        End Function

        ''' <summary>
        ''' 変換用Dictionaryを取得するための内部処理
        ''' </summary>
        ''' <param name="isDefaultToProperty"></param>
        Private Function GetNameConvertDic(Optional ByVal isDefaultToProperty As Boolean = True) As Dictionary(Of String, String)
            If isDefaultToProperty Then
                Return _convertDictionary
            Else
                Dim opposit As New Dictionary(Of String, String)
                For Each item As KeyValuePair(Of String, String) In _convertDictionary
                    opposit.Add(item.Value, item.Key) '逆にする
                Next
                Return opposit
            End If

        End Function

    End Class

End Namespace

# HttpServer

## 概要
デフォルトでは、ポート8080でリクエストを待ち受ける。（環境変数「UriPrefix」の設定）
「UriPrefix」が指定されていない場合は、処理を終了する。
リクエストがPOSTメソッドでない場合やリクエストのボディが空の場合は、処理を終了する。
リクエストのヘッダから「additionalData」というキーで値を取得する。
値が空でなければ、JSON からオブジェクトに変換して保持する。
リクエストのボディからメッセージオブジェクトを生成する。
上で生成したメッセージオブジェクトのプロパティに「additionalData」の中のキーと値を設定する。
「output」という名前でメッセージを送信する。
呼び出し元にはレスポンス「200 OK」を返却する。（リクエストの内容にかかわらず）

## Quick Start

## Feedback
お気づきの点があれば、ぜひIssueにてお知らせください。

## LICENSE
This project is licensed under the MIT License, see the LICENSE file for details

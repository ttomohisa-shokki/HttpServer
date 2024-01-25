# HttpServer

## 概要
HttpServerは、HTTPリクエストを受信して、メッセージとして送信します。

具体的には、以下のような処理を実行します。

① 受信メッセージのプロパティで指定されたプロセスを実行する

② 内部でアプリケーションを呼び出す

③ 外部アプリケーションのレスポンスを受け取る

④ 後処理としてメッセージを送信する。その際に次に実行するプロセス名を指定して別のプロセスを再実行することも可能

![image](https://github.com/Project-GAUDI/ApplicationController/assets/148841312/cfb510cf-0175-4921-82e7-e4d76949204e)

## Quick Start

## Feedback
お気づきの点があれば、ぜひIssueにてお知らせください。

## LICENSE
This project is licensed under the MIT License, see the LICENSE file for details.

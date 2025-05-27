# HttpServer

## 目次
* [概要](#概要)
* [機能](#機能)
* [Quick Start](#quick-start)
* [イメージのURL](#イメージのurl)
* [動作保証環境](#動作保証環境)
* [Deployment 設定値](#deployment-設定値)
  * [環境変数](#環境変数)
  * [Desired Properties](#desired-properties)
  * [Create Option](#create-option)
  * [startupOrder](#startuporder)
* [受信メッセージ](#受信メッセージ)
  * [受信HTTPリクエスト](#受信httpリクエスト)
* [送信メッセージ](#送信メッセージ)
  * [送信HTTPレスポンス](#送信httpレスポンス)
  * [Message Body](#message-body)
  * [Message Properties](#message-properties)
* [Direct Method](#direct-method)
  * [SetLogLevel](#setloglevel)
  * [GetLogLevel](#getloglevel)
* [ログ出力内容](#ログ出力内容)
* [ユースケース](#ユースケース)
  * [ケース ①](#Usecase1)
* [Feedback](#feedback)
* [LICENSE](#license)

## 概要
HttpServerは、HTTPリクエストを受信して、メッセージとして送信するAzure IoT edgeモジュールです。

## 機能

* デフォルトでは、ポート8080でリクエストを待ち受ける。（環境変数「UriPrefix」の設定）
* 「UriPrefix」が指定されていない場合は、処理を終了する。
* リクエストがPOSTメソッドでない場合やリクエストのボディが空の場合は、処理を終了する。
* リクエストのヘッダから「additionalData」というキーで値を取得する。<br> 値が空でなければ、JSON からオブジェクトに変換して保持する。
* リクエストのボディからメッセージオブジェクトを生成する。
* 上で生成したメッセージオブジェクトのプロパティに「additionalData」の中のキーと値を設定する。
* 「output」という名前でメッセージを送信する。
* 呼び出し元にはレスポンス「200 OK」を返却する。（リクエストの内容にかかわらず）

![schematic diagram](./docs/img/schematic_diagram.drawio.png)

## Quick Start
1. Personal Access tokenを作成
（参考: [個人用アクセス トークンを管理する](https://docs.github.com/ja/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens)）

2. リポジトリをクローン
```
git clone https://github.com/Project-GAUDI/HttpServer.git
```

3. ./src/nuget_template.configの<GITHUB_USERNAME>と<PERSONAL_ACCESS_TOKEN>を自身のユーザー名とPersonal Access tokenに書き換えて、ファイル名をnuget.configに変更してください

4. Dockerイメージをビルド
```
docker image build -t <IMAGE_NAME> ./HttpServer/src/
```
例）
```
docker image build -t ghcr.io/<YOUR_GITHUB_USERNAME>/httpserver:<VERSION> ./HttpServer/src/
```

5. Dockerイメージをコンテナレジストリにプッシュ
```
docker push <IMAGE_NAME>
```
例）
```
docker push ghcr.io/<YOUR_GITHUB_USERNAME>/httpserver:<VERSION>
```

6. Azure IoT edgeで利用

## イメージのURL
準備中
| URL                                                        | Description         |
| ---------------------------------------------------------- | ------------------- |

## 動作保証環境

| Module Version | IoTEdge         | edgeAgent       | edgeHub         | amd64 verified on | arm64v8 verified on | arm32v7 verified on |
| -------------- | --------------- | --------------- | --------------- | ----------------- | ------------------- | ------------------- |
| 6.0.2          | 1.5.0<br>1.5.16 | 1.5.6<br>1.5.19 | 1.5.6<br>1.5.19 | ubuntu22.04       | －                  | －                  |

## Deployment 設定値

### 環境変数

#### 環境変数の値

| Key                       | Required | Default | Recommend | Description                                                     |
| ------------------------- | -------- | ------- | --------- | ---------------------------------------------------------------- |
| UriPrefix                 | 〇       |         |           | リクエストを待ち受けるポートを指定する。 <br>例）http://+:8080/ <br> （「+」の箇所は状況に応じてホスト名やIPアドレスでも可） |
| TransportProtocol         |          | Amqp    |           | ModuleClient の接続プロトコル。<br>["Amqp", "Mqtt"] |
| LogLevel                  |          | info    |           | 出力ログレベル。<br>["trace", "debug", "info", "warn", "error"] |

### Desired Properties

#### Desired Properties の値

| JSON Key | Type   | Required | Default | Recommend | Description                  |
| -------- | ------ | -------- | ------- | --------- | ---------------------------- |
| output   | string |          | output  |           | 送信するメッセージのoutput名 |

#### Desired Properties の記入例

```json
{
  "output": "output1"
}
```

### Create Option

#### Create Option の値

| JSON Key                      | Type   | Required | Description                    |
| ----------------------------- | ------ | -------- | ------------------------------ |
| ExposedPorts                  | object | △        | モジュール内の公開ポート設定。ポートバインドに8080を使用する場合は不要。      |
| &nbsp;  xxxx/tcp              | object | △        | 公開したいポート番号(xは任意の番号)。 |
| &nbsp; &nbsp; {}              | object | △        | 値は不要。      |
| HostConfig                    | object | 〇        |                                |
| &nbsp; PortBindings           | object | 〇        | ポートバインド設定             |
| &nbsp; &nbsp; 8080/tcp        | object | 〇        | モジュール内の公開ポート       |
| &nbsp; &nbsp; &nbsp; HostPort | string | 〇        | デバイスにマップするポート番号 |

※開放するポートによって、「"8080/tcp"」（モジュール側）と「"HostPort": "80"」（ホスト側）の値を変更すること <br>

#### Create Option の記入例

```json
{
  "HostConfig": {
    "PortBindings": {
      "8080/tcp": [
        {
          "HostPort": "80"
        }
      ]
    }
  }
}
```

モジュール内部ポートに8080以外を使用する場合。

```json
{
  "ExposedPorts":{
    "8081/tcp":{}
  },
  "HostConfig": {
    "PortBindings": {
      "8081/tcp": [
        {
          "HostPort": "80"
        }
      ]
    }
  }
```
### startupOrder

#### startupOrder の値

| JSON Key      | Type    | Required | Default | Recommend | Description |
| ------------- | ------- | -------- | ------- | --------- | ----------- |
| startupOrder  | uint    |  | 4294967295 | 200 | モジュールの起動順序。数字が小さいほど先に起動される。<br>["0"から"4294967295"] |

#### startupOrder の記入例

```json
{
  "startupOrder": 200
}
```

## 受信メッセージ

### 受信HTTPリクエスト

| Content               | Description             |
| --------------------- | ----------------------- |
| メソッド              | POST                    |
| ヘッダー              | リクエストヘッダー      |
| &nbsp; additionalData | メッセージの properties(※1) |
| 本文                  | メッセージの body       |

※1 additionalData項目自体を複数指定する事はできない。<br>複数のメッセージプロパティを付与する場合、まとめて指定する必要がある。<br>例）
```JSON
   additionalData：{"prop1":"aaa", "prop2":"123"}
```


## 送信メッセージ

### 送信HTTPレスポンス

HTTPリクエストの送信元に返すレスポンス(リクエストの内容に関わらず同一)。

| Content              | Description |
| -------------------- | ----------- |
| プロトコルバージョン | 1.1         |
| ステータスコード     | 200         |
| テキストフレーズ     | "OK"        |

### Message Body

受信したHTTPリクエストの本文をそのまま送信する

### Message Properties

| Key | Description                                                      |
| --- | ---------------------------------------------------------------- |
| -   | リクエストのヘッダ「additionalData」キーの中身。名前や数は不定。 |

## Direct Method

### SetLogLevel

* 機能概要

  実行中に一時的にLogLevelを変更する。<br>
  変更はモジュール起動中または有効時間を過ぎるまで有効。<br>

* payload

  | JSON Key      | Type    | Required | default | Description |
  | ------------- | ------- | -------- | -------- | ----------- |
  | EnableSec     | integer  | 〇       |          | 有効時間(秒)。<br>-1:無期限<br>0:リセット(環境変数LogLevel相当に戻る)<br>1以上：指定時間(秒)経過まで有効。  |
  | LogLevel      | string  | △       |          | EnableSec=0以外を指定時必須。指定したログレベルに変更する。<br>["trace", "debug", "info", "warn", "error"]  |

  １時間"trace"レベルに変更する場合の設定例

  ```json
  {
    "EnableSec": 3600,
    "LogLevel": "trace"
  }
  ```

* response

  | JSON Key      | Type    | Description |
  | ------------- | ------- | ----------- |
  | status          | integer | 処理ステータス。<br>0:正常終了<br>その他:異常終了         |
  | payload          | object  | レスポンスデータ。         |
  | &nbsp; CurrentLogLevel | string  | 設定後のログレベル。（正常時のみ）<br>["trace", "debug", "info", "warn", "error"]  |
  | &nbsp; Error | string  | エラーメッセージ（エラー時のみ）  |

  ```json
  {
    "status": 0,
    "paylaod":
    {
      "CurrentLogLevel": "trace"
    }
  }
  ```

### GetLogLevel

* 機能概要

  現在有効なLogLevelを取得する。<br>
  通常は、LogLevel環境変数の設定値が返り、SetLogLevelで設定した有効時間内の場合は、その設定値が返る。<br>

* payload

  なし

* response

  | JSON Key      | Type    | Description |
  | ------------- | ------- | ----------- |
  | status          | integer | 処理ステータス。<br>0:正常終了<br>その他:異常終了         |
  | payload          | object  | レスポンスデータ。         |
  | &nbsp; CurrentLogLevel | string  | 現在のログレベル。（正常時のみ）<br>["trace", "debug", "info", "warn", "error"]  |
  | &nbsp; Error | string  | エラーメッセージ（エラー時のみ）  |

  ```json
  {
    "status": 0,
    "paylaod":
    {
      "CurrentLogLevel": "trace"
    }
  }
  ```

## ログ出力内容

| LogLevel | 出力概要 |
| -------- | -------- |
| error    | [初期化/desired更新/desired取り込み/メッセージ受信]失敗         |
| warn     | エッジランタイムとの接続リトライ失敗<br>環境変数の1部値不正         |
| info     | 環境変数の値<br>desired更新通知<br>環境変数の値未設定のためDefault値適用<br>メッセージ[送信/受信]通知         |
| debug    | 無し     |
| trace    | メソッドの開始・終了<br>受信メッセージBody  |

## ユースケース

<a id="Usecase1"></a>

### ケース①

「IoT Edge Device-1」から「IoT Edge Device-2」へHttpClient・HttpServerを使用してメッセージを転送する。

#### 受信HTTPリクエスト例

```JSON
＜Header＞
  additionalData：{"prop1":"aaa"}

＜Body＞
  {
    "RecordList":[{
      "RecordHeader": [
        "2020/11/11  12:00:00"
      ],
      "RecordData": [
        30, 40, 100000
      ]
    }]
  }
```

### 出力結果

HttpClientが受信したものがそのまま送信メッセージとなる。

#### 出力例

```JSON
＜プロパティ＞
  {"prop1":"aaa"}

＜Body＞
  {
    "RecordList":[{
      "RecordHeader": [
        "2020/11/11  12:00:00"
      ],
      "RecordData": [
        30, 40, 100000
      ]
    }]
  }
```

## Feedback
お気づきの点があれば、ぜひIssueにてお知らせください。

## LICENSE
HttpServer is licensed under the MIT License, see the [LICENSE](LICENSE) file for details.

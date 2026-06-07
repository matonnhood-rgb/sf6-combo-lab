# ChatGPT から GitHub 経由で進捗を読むための手順

## 目的

このプロジェクトを GitHub に置き、ChatGPT の GitHub 連携からコード、README、進捗メモを読めるようにします。

## ローカル側でやること

1. このフォルダを Git リポジトリにする
2. GitHub に空のリポジトリを作る
3. リモート URL を登録する
4. 変更を commit して GitHub に push する

例:

```powershell
git init
git add .
git commit -m "Initial Combo Lab project"
git branch -M main
git remote add origin https://github.com/<ユーザー名>/<リポジトリ名>.git
git push -u origin main
```

## ChatGPT 側でやること

1. ChatGPT の Settings を開く
2. Apps から GitHub を選ぶ
3. GitHub に移動して ChatGPT アプリをインストール・認可する
4. このプロジェクトのリポジトリへのアクセスを許可する
5. 反映されるまで数分待つ

OpenAI の公式ヘルプでは、ChatGPT は接続済み GitHub リポジトリ内のコード、README、その他ドキュメントを取得して参照できます。
リポジトリがすぐ表示されない場合は、GitHub 側の検索で `repo:<ユーザー名>/<リポジトリ名> import` を検索すると、インデックス作成を促せる場合があります。

## 運用ルール

- 進捗の入口は `PROGRESS.md` にする
- 大きな変更をしたら `PROGRESS.md` の「最近の状態」を更新する
- 詳しい仕様や調査メモは `docs/` に置く
- ChatGPT に質問するときは「まず `PROGRESS.md` を読んで」と伝える

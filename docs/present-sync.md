# Present同期再生

Present同期再生は、手動でテスト再生を開始したときに、Combo Lab側の0Fと
ゲーム側の表示フレームの位相が毎回ずれる問題を軽減するための機能です。

この機能は、PresentMonコンソール版のCSV/STDOUT出力から対象プロセスの
`Present()` 時刻を取得し、その時刻を基準に入力境界イベントを送信します。
ゲーム内部メモリ読み取り、プロセス注入、DirectXフック、アンチチート回避は
行いません。

## Present時刻について

ここで使うPresentは、ゲームが入力を読み込む瞬間そのものではありません。
取得しているのは、公開ツールで観測できる `Present()` 呼び出し時刻です。

そのため、Present同期は手動開始による大きな位相ずれをかなり軽減できますが、
入力受付タイミングを完全に保証するものではありません。

## 入力位相補正

`入力位相補正ms` は、環境ごとの固定補正として使います。

- マイナス値: Present予測時刻より少し早く送る
- プラス値: Present予測時刻より少し遅く送る

補正はすべての境界イベントへ一律で適用します。KeyUpだけ、またはKeyDownだけに
別補正は掛けません。

VSync、VRR、フルスクリーン、ボーダレス、表示Hz、GPU設定が変わると、
必要な補正値も変わる可能性があります。

## 1F保持について

1F保持は、ゲーム側の入力ポーリングやWindows側のスケジューリングによって
不安定になることがあります。実用では、攻撃ボタンは2F～3F保持から試すことを
推奨します。

## PresentMonが起動できない場合

次を確認してください。

- `PresentMon exe path` が正しいか
- 対象のprocess idまたはprocess nameが正しいか
- 対象ゲームが起動しているか
- access deniedが出る場合、権限、Performance Log Users、管理者実行を検討
- CSV出力に `QPCTime` 列があるか

Combo Labは `QPCTime` が取れる出力だけをPresent同期に使います。
`CPUStartQPC` や `CPUStartQPCTime` しかない場合、それをPresent時刻として
黙って流用せず、同期不可として扱います。

## 境界イベント

アクション定義の入力イベントは「そのFで押されているべきキー状態」として扱います。
Combo Labは、前状態と次状態の差分から境界イベントを作ります。

例:

```text
10F: 4
11F: 4, 3
12F: 3, I, 6
```

生成される境界:

```text
10F Boundary: Down 4
11F Boundary: Down 3
12F Boundary: Up 4 / Down I, 6
13F Boundary: Up 3, I, 6
```

同じ境界でKeyUpとKeyDownが必要な場合、SendInput配列内では
KeyUpを先、KeyDownを後に並べます。

## ログ

Present同期再生のログには、次の情報を出します。

- logicalFrame
- targetPresent
- qpcTarget
- qpcActual
- targetMs
- actualMs
- lateMs
- stateAfter
- SendInputの送信結果

`lateMs` が大きい場合、OSスケジューリングや負荷の影響で予定より遅れている
可能性があります。

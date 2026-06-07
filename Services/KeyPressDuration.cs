namespace ComboLab.Services;

public readonly record struct KeyPressDuration(TimeSpan Value)
{
    public static KeyPressDuration FromFrames(int frames, double framesPerSecond = 60)
    {
        if (frames <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frames),
                "押下フレーム数は1以上にしてください。");
        }

        return new KeyPressDuration(
            TimeSpan.FromSeconds(frames / framesPerSecond));
    }

    public static KeyPressDuration FromMilliseconds(double milliseconds)
    {
        if (milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(milliseconds),
                "押下時間は0ミリ秒より大きくしてください。");
        }

        return new KeyPressDuration(TimeSpan.FromMilliseconds(milliseconds));
    }
}

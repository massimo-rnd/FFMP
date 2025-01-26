namespace FFMP;

public class ProgressBar : IDisposable
{
    private int _current = 0;
    private int _total;

    public ProgressBar(int total)
    {
        _total = total;
        Console.WriteLine("Progress:");
    }

    public void Report(double value)
    {
        _current += (int)(value * _total);
        Console.Write($"\r[{new string('#', _current)}{new string(' ', _total - _current)}] {(_current * 100.0 / _total):F1}%");
    }

    public void Dispose()
    {
        Console.WriteLine();
    }
}
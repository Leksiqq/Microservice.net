namespace Net.Leksi.MicroService;

public class HeadersHolder
{
    public Dictionary<string, object> Headers { get; private init; } = [];

    public void AddHeader(string key, object value)
    {
        if (!Headers.TryGetValue(key, out object? val))
        {
            Headers.Add(key, value);
        }
        else
        {
            if (val is List<object> list)
            {
                list.Add(value);
            }
            else
            {
                Headers[key] = new List<object> { val, value };
            }
        }
    }

}

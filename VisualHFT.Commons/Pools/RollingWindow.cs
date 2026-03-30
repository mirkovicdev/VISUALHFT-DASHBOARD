
namespace VisualHFT.Commons.Pools
{
    public class RollingWindow<T>
    {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly int _maxSize;

        public RollingWindow(int size)
        {
            _maxSize = size;
        }

        public void Add(T item)
        {
            _queue.Enqueue(item);
            if (_queue.Count > _maxSize)
                _queue.Dequeue();
        }

        public IEnumerable<T> Items => _queue;

        public int Count => _queue.Count;

        public void Clear()
        {
            _queue.Clear();
        }

        public T Last()
        {
            return _queue.Last();
        }

        public bool Any()
        {
            return _queue.Any();
        }
    }
}

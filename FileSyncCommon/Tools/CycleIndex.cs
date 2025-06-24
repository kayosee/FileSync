namespace FileSyncCommon.Tools
{
    public class CycleIndex
    {
        private int _value;
        private int _max;

        public CycleIndex(uint max)
        {
            _value = 0;
            _max = (int)max;
        }

        public int Value => Interlocked.CompareExchange(ref _value, 0, 0);

        public int GetAndNext()
        {
            int original;
            int nextValue;

            do
            {
                // 获取当前值
                original = _value;

                // 计算下一个值（使用高效条件判断替代取模）
                nextValue = original + 1;
                if (nextValue >= _max)
                {
                    nextValue = 0;
                }
            }
            while (Interlocked.CompareExchange(
                    ref _value,
                    nextValue,
                    original) != original);

            return original;
        }
    }
}

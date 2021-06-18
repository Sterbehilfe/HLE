﻿namespace Sterbehilfe.Utils.Time
{
    public interface ITimeUnit
    {
        public int Count { get; set; }

        public long ToMilliseconds();

        public long ToSeconds();
    }
}
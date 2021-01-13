using System;
using System.Collections.Generic;
using System.Text;

namespace ImageProcessingLib
{
    struct Pair : IComparable<Pair>
    {
        public Pair(int number, int i, int j)
        {
            Number = number;
            I = i;
            J = j;
        }
        public int Number;
        public int I;
        public int J;

        public int CompareTo(Pair other)
        {
            return this.Number - other.Number;
        }
    }
}

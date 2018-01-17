using System;

namespace UniversalTennis.Algorithm
{
    public class RatingException : Exception
    {
        public RatingException(string message)
            : base(message)
        {
        }
    }
}

using System;

namespace CheckConsistency
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) :
            base(message)
        {
            
        }
    }
}
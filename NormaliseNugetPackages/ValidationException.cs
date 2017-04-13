using System;

namespace NormaliseNugetPackages
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) :
            base(message)
        {
            
        }
    }
}
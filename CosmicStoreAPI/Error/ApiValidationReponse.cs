namespace CosmicStoreAPI.Error
{
    public class ApiValidationResponse:ApiErrorResponse
    {
        public ApiValidationResponse() : base(400)
        {
            Errors = [];
        }

        public ApiValidationResponse(IEnumerable<string> errors):base(400)
        {
            Errors = errors;
        }

        public IEnumerable<string> Errors { get; set; }
    }
}
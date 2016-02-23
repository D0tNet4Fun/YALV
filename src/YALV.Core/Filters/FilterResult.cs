namespace YALV.Core.Filters
{
    public enum FilterResult
    {
        /// <summary>
        /// The item needs to be excluded at once.
        /// </summary>
        Exclude,
        /// <summary>
        /// The item can be excluded unless another filter requests oterwise.
        /// </summary>
        CanExclude,
        /// <summary>
        /// The item needs to be included unless another filter requests otherwise.
        /// </summary>
        Include,
        /// <summary>
        /// The item was procesed by an exclusive filter and it was not excluded.
        /// </summary>
        Ignore
    }
}
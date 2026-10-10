namespace SprocketShellSelector;

internal sealed record WireOptions(string Transport="current",double MaximumLength=4000,double RetentionSeconds=20,double DisplayWidth=.008,double SampleDistance=2)
{
    internal void Validate(string id)
    {
        if(Transport is not ("current" or "wire"))throw new FormatException($"Profile '{id}': guidanceTransport must be current or wire.");
        void Range(string field,double value,double min,double max)
        {if(!double.IsFinite(value)||value<min||value>max)throw new FormatException(ShellBallistics.RangeError(id,field,value,min,max));}
        Range("wireMaximumLength",MaximumLength,10,20000);
        Range("wireRetentionSeconds",RetentionSeconds,0,60);
        Range("wireDisplayWidth",DisplayWidth,.001,.05);
        Range("wireSampleDistance",SampleDistance,.1,100);
    }
}

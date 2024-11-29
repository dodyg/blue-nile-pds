using atompds.OauthProvider.Util;

namespace atompds.OauthProvider;

public record DeviceId
{
    public readonly int DEVICE_ID_LENGTH = Constants.DEVICE_ID_PREFIX.Length + Constants.DEVICE_ID_BYTES_LENGTH * 2; // hex encoding
    
    public string Value { get; }
    
    public DeviceId(string deviceId)
    {
      if (deviceId.Length != DEVICE_ID_LENGTH)
      {
        throw new ArgumentException("Invalid device ID format");
      }
      
      if (!deviceId.StartsWith(Constants.DEVICE_ID_PREFIX))
      {
        throw new ArgumentException("Invalid device ID format");
      }
      
      Value = deviceId;
    }
    
    public static DeviceId GenerateDeviceId()
    {
      return new DeviceId($"{Constants.DEVICE_ID_PREFIX}{Crypto.RandomHexId(Constants.DEVICE_ID_BYTES_LENGTH)}");
    }
    
    public override string ToString()
    {
      return Value;
    }
    
    public static implicit operator string(DeviceId deviceId)
    {
      return deviceId.Value;
    }
    
    public static implicit operator DeviceId(string deviceId)
    {
      return new DeviceId(deviceId);
    }
}
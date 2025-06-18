using System.Xml.Serialization;

namespace RoEFactura;

public class XmlDeserializer
{
    public T DeserializeXmlFromFile<T>(string filePath)
    {
        // Ensure the file exists
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        // Initialize the XmlSerializer
        XmlSerializer serializer = new XmlSerializer(typeof(T));

        // Open the file
        using FileStream fileStream = new FileStream(filePath, FileMode.Open);
        // Deserialize the file content to the object
        T result = (T)serializer.Deserialize(fileStream);
        return result;
    }
}
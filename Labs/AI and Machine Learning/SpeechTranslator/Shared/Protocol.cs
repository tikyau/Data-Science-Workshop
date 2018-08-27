using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MT.Api
{
    //
    // Messages from Server to Client 
    //

    /// <summary>
    /// Defines a partial result.
    /// </summary>
    [DataContract]
    public class PartialResultMessage
    {
        /// Message type identifier.
        [DataMember(Name = "type")]
        public string Type = "partial";
        /// Partial result "major.minor" identifier (e.g. "23.4").
        [DataMember(Name = "id")]
        public string Id;
        /// Recognized text.
        [DataMember(Name = "recognition")]
        public string Recognition;
        /// Translation of the recognized text.
        [DataMember(Name = "translation", EmitDefaultValue = false)]
        public string Translation;
    }

    
    /// <summary>
    /// Defines a final result.
    /// </summary>
    [DataContract]
    public class FinalResultMessage
    {
        /// Message type identifier.
        [DataMember(Name = "type")]
        public string Type = "final";
        /// Partial result "major" identifier.
        [DataMember(Name = "id")]
        public string Id;
        /// Recognized text.
        [DataMember(Name = "recognition")]
        public string Recognition;
        /// Translation of the recognized text.
        [DataMember(Name = "translation", EmitDefaultValue = false)]
        public string Translation;
    }

   
}

using System.ComponentModel.DataAnnotations;

namespace CineGT.Models
{
    public class ProcedureParameter
    {
        public ProcedureParameter()
        {

        }
        public string ProcedureName { get; set; }
        public string ParameterName { get; set; }
        public string DataType { get; set; }
        public int MaxLength { get; set; }
        public bool IsOutput { get; set; }
        public string Value { get; set; }
    }

}

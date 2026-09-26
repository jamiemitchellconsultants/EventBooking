using Microsoft.AspNetCore.Components;
using System.Security.Cryptography;
using System.Text;

namespace EventBooking.Web.Components;

public partial class TypeChip
{
    [Parameter, EditorRequired] public string Code { get; set; } = "";
    [Parameter, EditorRequired] public string Name { get; set; } = "";
    private static int PaletteFor(string code) =>
        (int)(SHA256.HashData(Encoding.UTF8.GetBytes(code.ToUpperInvariant()))[0] % 8);
}

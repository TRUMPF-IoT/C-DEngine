# Enapter EL4.1 Error Codes Dictionary

## Overview
This file contains the C# dictionary for Enapter EL4.1 Electrolyser Warning, Error, and Fatal Error Codes.

## Data Source
The error codes should be populated from the official Enapter EL4.1 handbook:
https://handbook.enapter.com/electrolyser/el41/#warning-error-and-fatal-error-codes

## How to Populate

1. Visit the Enapter EL4.1 handbook link above
2. Locate the "Warning, Error and Fatal Error Codes" table
3. Open the file `EnapterEL41ErrorCodes.cs`
4. Add each error code entry in the dictionary initialization:

```csharp
public static readonly IReadOnlyDictionary<string, string> ErrorCodeDescriptions = new ReadOnlyDictionary<string, string>(
    new Dictionary<string, string>
    {
        { "W1", "Warning description from handbook" },
        { "W2", "Another warning description" },
        { "E1", "Error description from handbook" },
        { "F1", "Fatal error description from handbook" },
        // ... add all codes from the table
    });
```

## Dictionary Format
- **Key**: Error code (e.g., "W1", "E5", "F3")
- **Value**: Description of the error/warning

## Usage Examples

### Get a description for an error code:
```csharp
string description = EnapterEL41ErrorCodes.GetDescription("W1");
if (description != null)
{
    Console.WriteLine($"Error W1: {description}");
}
```

### Check if a code is valid:
```csharp
if (EnapterEL41ErrorCodes.IsValidCode("E5"))
{
    Console.WriteLine("E5 is a valid error code");
}
```

### Get all available codes:
```csharp
var allCodes = EnapterEL41ErrorCodes.GetAllCodes();
foreach (var code in allCodes)
{
    Console.WriteLine($"{code}: {EnapterEL41ErrorCodes.GetDescription(code)}");
}
```

## Testing
After populating the dictionary, update the tests in:
`/src/Tests/C-DEngine.Tests/C-DBaseClasses/EnapterEL41ErrorCodesTests.cs`

Add specific test cases for the actual error codes, for example:
```csharp
[Test]
public void GetDescription_WithValidWarningCode_ReturnsDescription()
{
    var result = EnapterEL41ErrorCodes.GetDescription("W1");
    Assert.That(result, Is.Not.Null);
    Assert.That(result, Contains.Substring("temperature")); // or whatever the actual description says
}
```

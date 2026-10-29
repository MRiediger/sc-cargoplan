using CargoPlan.Core.Planning;

namespace CargoPlan.App.ViewModels;

/// <summary>One entry of the settings window's list for gone contracts (D81): the value for the section, the text for the player.</summary>
public sealed record ContractGoneChoice(ContractGonePolicy Value, string Text);

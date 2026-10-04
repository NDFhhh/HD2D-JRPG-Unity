using MFramework.Event;

public readonly struct ChooseCharacterButtonOnClickEvent : IEvent
{
    public readonly CharacterIdentity CharacterIdentity;

    public ChooseCharacterButtonOnClickEvent(CharacterIdentity characterIdentity)
    {
        CharacterIdentity = characterIdentity;
    }
}

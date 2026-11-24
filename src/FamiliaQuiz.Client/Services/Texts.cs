using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Client.Services;

/// <summary>pt-BR texts for server data (the hub only sends codes, never UI strings).</summary>
public static class Texts
{
    public static string Error(ErrorCode code) => code switch
    {
        ErrorCode.RoomNotFound => "Sala não encontrada. Confira o código.",
        ErrorCode.RoomFull => "A sala está cheia.",
        ErrorCode.InvalidName => $"Digite um nome (até {GameLimits.MaxNameLength} letras).",
        ErrorCode.NameTaken => "Já tem alguém com esse nome na sala. Escolha outro.",
        ErrorCode.InvalidStepCount => $"A escada precisa ter de {GameLimits.MinSteps} a {GameLimits.MaxSteps} degraus.",
        ErrorCode.InvalidSession => "Não foi possível voltar para a sala. Entre de novo.",
        ErrorCode.NotInRoom => "Você não está numa sala.",
        ErrorCode.NotHost => "Só quem criou a sala pode fazer isso.",
        ErrorCode.NotKing => "Só o Rei pode fazer isso.",
        ErrorCode.KingCannotGuess => "O Rei não chuta, ele sabe a resposta! 😉",
        ErrorCode.WrongPhase => "Agora não dá para fazer isso.",
        ErrorCode.NotEnoughPlayers => "Precisa de pelo menos 2 jogadores.",
        ErrorCode.PlayerNotFound => "Jogador não encontrado.",
        ErrorCode.InvalidQuestion => $"A pergunta precisa de um texto e de {GameLimits.MinOptions} a {GameLimits.MaxOptions} opções diferentes.",
        ErrorCode.InvalidOption => "Opção inválida.",
        ErrorCode.InvalidNextKing => "Escolha quem vai ser o próximo Rei.",
        _ => "Ops, algo deu errado. Verifique a conexão e tente de novo.",
    };
}

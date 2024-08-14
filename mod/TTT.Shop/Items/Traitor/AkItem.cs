using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using TTT.Player;
using TTT.Public.Mod.Role;
using TTT.Public.Shop;

namespace TTT.Shop.Items.Traitor;

public class AkItem : IShopItem
{
    public string Name()
    {
        return "AK-47";
    }

    public string SimpleName()
    {
        return "ak47";
    }

    public int Price()
    {
        return 500;
    }

    public BuyResult OnBuy(GamePlayer player)
    {
        if (player.Credits() < Price()) return BuyResult.NotEnoughCredits;
        if (player.PlayerRole() != Role.Traitor) return BuyResult.IncorrectRole;
        CCSPlayerController? playerController = player.Player();

        player.RemoveCredits(Price());
        playerController?.GiveNamedItem(CsItem.AK47);

        return BuyResult.Successful;
    }
}
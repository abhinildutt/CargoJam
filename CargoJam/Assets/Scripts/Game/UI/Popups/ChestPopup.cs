using System;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The chest reward: a chest over slowly turning rays, "Tap to open". A tap winds it up, it
    /// bursts open (a reward moment: overshoot allowed), the rewards pop out one by one, and Collect
    /// flies the coins home.
    /// </summary>
    public sealed class ChestPopup : Popup
    {
        readonly Icon rays;
        readonly Icon chest;
        readonly Label tapHint;
        readonly VisualElement rewards;
        readonly VisualElement actions;
        readonly CandyButton collect;
        Func<ChestReward> open;
        Action done;
        UiTween raysLoop, shake;
        bool opened;
        bool holdingCoins;
        VisualElement coinItem;
        int coins;

        public ChestPopup(UiContext ctx) : base(ctx, "Chest", PopupKind.Card)
        {
            var stage = Ui.El(Body, "cj-chest__stage", true);
            rays = Ui.Icon(stage, IconKind.Rays, "cj-chest__rays");
            rays.Tint = UiPalette.A(UiPalette.Gold, 0.45f);
            chest = Ui.Icon(stage, IconKind.Chest, "cj-chest__icon");
            tapHint = Ui.Text(Body, "Tap to open", "cj-t-label cj-c-ink2 cj-chest__hint");
            rewards = Ui.El(Body, "cj-chest__rewards");
            actions = Ui.El(Body, "cj-popup__actions");
            collect = new CandyButton("Collect", "cj-btn--primary cj-btn--l cj-btn--wide");
            collect.Clicked += OnCollect;
            actions.Add(collect);
            stage.RegisterCallback<PointerUpEvent>(_ => Open());
        }

        /// <param name="big">Shows the big-chest styling.</param>
        /// <param name="openAction">Grants the chest (Progression.OpenChest); may return null.</param>
        /// <param name="doneAction">Runs after the popup closes.</param>
        public void Show(bool big, Func<ChestReward> openAction, Action doneAction)
        {
            open = openAction;
            done = doneAction;
            opened = false;
            Title = big ? "Big chest!" : "Chest";
            chest.Kind = IconKind.Chest;
            chest.EnableInClassList("cj-chest__icon--big", big);
            rewards.Clear();
            Ui.Show(rewards, false);
            Ui.Show(actions, false);
            Ui.Show(tapHint, true);
            Anim.SetOpacity(tapHint, 1f);
            Anim.SetRotate(chest, 0f);

            OpenPopup();
            Anim.PopInReward(chest, 0.12f);
            raysLoop?.Cancel();
            raysLoop = UiTween.Loop(t => Anim.SetRotate(rays, t * 12f));
        }

        void Open()
        {
            if (opened || !IsOpen) return;
            opened = true;
            Anim.Fade(tapHint, 1f, 0f, 0.15f, 0f, () => Ui.Show(tapHint, false));
            UiFeedback.Press();

            // Wind-up: a quickening wobble, then the burst.
            shake = UiTween.Run(0.6f, t =>
            {
                float amp = Mathf.Lerp(2f, 10f, t);
                Anim.SetRotate(chest, Mathf.Sin(t * 50f) * amp);
                Anim.SetScale(chest, 1f + t * 0.08f);
            }, null, 0f, Burst);
        }

        void Burst()
        {
            Anim.SetRotate(chest, 0f);
            chest.Kind = IconKind.ChestOpen;
            Anim.Punch(chest, 0.18f, 0.35f);
            Ctx.Fly.Burst(chest.worldBound.center, 22, 300f);
            UiFeedback.Reward();

            // Granting changes the balance; hold the counter so the coins can fly into it.
            Ctx.Coins.Freeze();
            holdingCoins = true;
            var reward = open?.Invoke();
            open = null;
            ShowRewards(reward);
        }

        void ShowRewards(ChestReward reward)
        {
            Ui.Show(rewards, true);
            coins = reward?.Coins ?? 0;
            coinItem = null;
            int i = 0;
            if (coins > 0) coinItem = AddReward(IconKind.Coin, "+" + Anim.Format(coins), i++, false);
            if (reward?.Boosters != null)
            {
                for (int b = 0; b < reward.Boosters.Length; b++)
                {
                    if (reward.Boosters[b] <= 0) continue;
                    AddReward(UiText.BoosterIcon((BoosterType)b), "×" + reward.Boosters[b], i++, true);
                }
            }
            UiTween.Delay(0.3f + i * 0.15f, () =>
            {
                Ui.Show(actions, true);
                Anim.PopIn(actions);
            });
        }

        VisualElement AddReward(IconKind kind, string text, int index, bool tinted)
        {
            var item = Ui.El(rewards, "cj-reward");
            var disc = Ui.El(item, "cj-reward__disc");
            var icon = Ui.Icon(disc, kind, "cj-reward__icon");
            if (tinted) icon.Tint = UiPalette.Ink;
            Ui.Text(item, text, "cj-t-heading cj-c-ink cj-reward__label");
            Anim.PopInReward(item, 0.2f + index * 0.15f);
            UiTween.Delay(0.2f + index * 0.15f, () => UiFeedback.CoinTick(index * 2));
            return item;
        }

        void OnCollect()
        {
            if (coins > 0 && coinItem != null)
                Ctx.Fly.Coins(coinItem.worldBound.center, Ctx.Coins, coins, FlyLayer.CoinCountFor(coins), 0f, ReleaseCoins);
            else
                ReleaseCoins();
            var after = done;
            done = null;
            Close(after);
        }

        void ReleaseCoins()
        {
            if (!holdingCoins) return;
            holdingCoins = false;
            Ctx.Coins.Unfreeze();
        }

        protected override void OnClosing()
        {
            raysLoop?.Cancel();
            shake?.Cancel();
            // If coins are mid-flight they release the counter when they land.
            if (coins <= 0) ReleaseCoins();
        }
    }
}

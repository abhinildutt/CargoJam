using System;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The chest reward: a chest over spinning rays, "Tap to open!". A tap shakes it harder and
    /// harder, it bursts open, the rewards pop out one by one, and Collect flies the coins home.
    /// </summary>
    public sealed class ChestPopup : Popup
    {
        readonly VisualElement stage;
        readonly Icon rays;
        readonly Icon chest;
        readonly Label tapHint;
        readonly VisualElement rewards;
        readonly CandyButton collect;
        Func<ChestReward> open;
        Action done;
        UiTween raysLoop, hintPulse, shake;
        bool opened;
        bool holdingCoins;
        VisualElement coinItem;
        int coins;

        public ChestPopup(UiContext ctx) : base(ctx, "Chest!", "cj-ribbon--coin", false)
        {
            stage = Ui.El(Body, "cj-chest__stage", true);
            rays = Ui.Icon(stage, IconKind.Rays, "cj-chest__rays");
            rays.Tint = new Color(1f, 0.85f, 0.35f, 1f);
            chest = Ui.Icon(stage, IconKind.Chest, "cj-chest__icon");
            tapHint = Ui.Text(Body, "Tap to open!", "cj-chest__hint cj-txt");
            rewards = Ui.El(Body, "cj-chest__rewards");
            collect = new CandyButton("Collect", "cj-btn--primary cj-btn--big");
            collect.Clicked += OnCollect;
            Body.Add(collect);
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
            Title = big ? "Big Chest!" : "Chest!";
            chest.Kind = IconKind.Chest;
            chest.EnableInClassList("cj-chest__icon--big", big);
            rewards.Clear();
            Ui.Show(rewards, false);
            Ui.Show(collect, false);
            Ui.Show(tapHint, true);
            Anim.SetRotate(chest, 0f);

            OpenPopup();
            Anim.PopIn(chest, 0.15f, 0.5f);
            raysLoop?.Cancel();
            raysLoop = UiTween.Loop(t => Anim.SetRotate(rays, t * 25f));
            hintPulse?.Cancel();
            hintPulse = Anim.Pulse(tapHint, 0.08f, 0.8f);
        }

        void Open()
        {
            if (opened || !IsOpen) return;
            opened = true;
            hintPulse?.Cancel();
            Anim.Fade(tapHint, 1f, 0f, 0.15f, 0f, () => Ui.Show(tapHint, false));
            UiFeedback.Press();

            // Wind-up: an accelerating wobble, then the burst.
            shake = UiTween.Run(0.75f, t =>
            {
                float amp = Mathf.Lerp(3f, 16f, t);
                Anim.SetRotate(chest, Mathf.Sin(t * 60f) * amp);
                Anim.SetScale(chest, 1f + t * 0.12f);
            }, null, 0f, Burst);
        }

        void Burst()
        {
            Anim.SetRotate(chest, 0f);
            chest.Kind = IconKind.ChestOpen;
            Anim.Punch(chest, 0.35f, 0.35f);
            Ctx.Fly.Burst(chest.worldBound.center, 28, 320f);
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
            if (coins > 0) coinItem = AddReward(IconKind.Coin, "+" + Anim.Format(coins), i++);
            if (reward?.Boosters != null)
            {
                for (int b = 0; b < reward.Boosters.Length; b++)
                {
                    if (reward.Boosters[b] <= 0) continue;
                    AddReward(UiText.BoosterIcon((BoosterType)b), "x" + reward.Boosters[b], i++);
                }
            }
            UiTween.Delay(0.3f + i * 0.15f, () =>
            {
                Ui.Show(collect, true);
                Anim.PopIn(collect);
            });
        }

        VisualElement AddReward(IconKind kind, string text, int index)
        {
            var item = Ui.El(rewards, "cj-reward");
            var disc = Ui.El(item, "cj-reward__disc");
            var icon = Ui.Icon(disc, kind, "cj-reward__icon");
            icon.Tint = IconPainter.Hex(0x4A90F0);
            Ui.Text(item, text, "cj-reward__label cj-txt");
            Anim.PopIn(item, 0.2f + index * 0.15f, 0.45f);
            UiTween.Delay(0.2f + index * 0.15f, () => UiFeedback.CoinTick(index * 2));
            return item;
        }

        void OnCollect()
        {
            if (coins > 0 && coinItem != null)
                Ctx.Fly.Coins(coinItem.worldBound.center, Ctx.Coins, coins, 12, 0f, ReleaseCoins);
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
            hintPulse?.Cancel();
            shake?.Cancel();
            // If coins are mid-flight they release the counter when they land.
            if (coins <= 0) ReleaseCoins();
        }
    }
}

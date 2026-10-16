# Dots and Boxes: design

Store title: **Dots and Boxes**. Package `com.renegadegames.dotsandboxes`.

## The game
A 5×5 grid of boxes (6×6 pegs, 60 lines). Players take turns drawing a line between two
neighbouring pegs; closing the fourth side of a box claims it and earns another turn. When every
line is drawn, the most boxes wins (25 boxes, so a game can't be tied).

## Feel
- Brass pegs on a board in the chosen material; tap between two pegs and a stick drops into place
  in your colour. Colours from the Style menu (Ruby & Sapphire by default).
- **Signature effect, box pops:** a closed box gets a glowing tile in the closer's colour that
  springs up into it with sparks. In a chain, each pop rings a little higher ("Box! ×4"), and the
  computer speeds up through a chain so it flows.
- Drag sideways to spin the board.

## Modes
Player vs player; vs computer Easy (takes boxes, otherwise random), Medium (never gives a box when
a safe line exists; gives the smallest chain when forced), Hard (Medium until 18 lines remain, then
an exact, memoised search to the end).

## Ads
Between-games interstitial every 5th game; Undo (ad) vs the computer takes back your last turn.

## Later
Grid sizes 3×3 to 7×7; a hint showing safe lines.

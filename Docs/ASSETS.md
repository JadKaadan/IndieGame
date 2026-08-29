# Asset register and licensing

Every external asset that enters this project gets a row in the table below,
filled in **before** it is committed.

**I have not verified the licence of any specific third-party asset in this
session, and I will not tell you an asset is safe to ship commercially unless its
licence actually says so.** What follows is the process, not a list of
pre-approved assets.

---

## Current assets

| Asset | Source | Creator | Licence | Attribution | Commercial | Modified | Purpose |
| --- | --- | --- | --- | --- | --- | --- | --- |
| *(none)* | — | — | — | — | — | — | Everything in the repo is original or a Unity primitive |

The prototype vehicle is built from Unity primitives and a fictional
specification. There is nothing to clear.

---

## Before adding any asset

Answer all seven, in writing, in the table:

1. Is it free, or what did it cost?
2. What licence exactly? (Name and version — "free download" is not a licence.)
3. Does the licence permit use in a **commercial** game?
4. Is attribution required, and in what form?
5. May the asset be **embedded in a compiled build**? Some licences permit
   downloading and editing but not redistribution, including inside a binary.
6. Are modifications permitted?
7. Are there trademark or brand restrictions separate from the copyright?

If you cannot answer all seven, the asset does not go in.

### Licences you will meet

| Licence | Commercial | Attribution | Notes |
| --- | --- | --- | --- |
| CC0 | Yes | No | Safest |
| CC-BY | Yes | Yes | Attribution must appear in the shipped game |
| **CC-BY-NC** | **No** | Yes | Non-commercial. Common on free car models. Unusable |
| **CC-BY-SA** | Yes | Yes | Share-alike may force you to license derivatives the same way |
| CC-BY-ND | Yes | Yes | No derivatives — you cannot even fix the pivots |
| Unity Asset Store EULA | Yes | No | Embedding permitted, redistribution as an asset is not |
| Sketchfab "Free" | **Varies per model** | Varies | Read the specific model's licence, never assume |

`CC-BY-NC` in particular is extremely common on free car models and is
**incompatible with a commercial release**.

---

## Vehicle branding is a separate problem

There is a difference between:

- having a 3D model, and
- having permission to use a manufacturer's name, logo, badges and vehicle
  design commercially.

Manufacturer names, logos, badges, grille designs and body shapes may be
protected by trademark and design rights **independently of who made the 3D
model**. A CC0 model of a real car does not grant you rights to that car's brand.

Downloading a free model of a real branded car does not give you permission to
ship it.

**Practical policy for this project:**

- Prototype and tune with whatever is legally available for development
- Ship with **fictional vehicles**, or with properly licensed real ones
- Because every car is a `VehicleDefinition` plus a prefab, swapping a prototype
  for a licensed model later is a content task, not a code task — which is
  exactly why the architecture is built this way
- If you intend to ship real branded vehicles, budget for licensing negotiation
  early. It is slow.

---

## Model quality checklist

Before committing to any vehicle model:

**Geometry**
- Correct real-world scale (a car is ~4.5 m long, ~1.4 m tall)
- Wheels are separate objects with pivots at the wheel centre
- Wheel local X is the axle axis (or can be corrected with a parent)
- Interior geometry exists — not a flat dashboard texture
- Instrument needles are separate objects with correct pivots
- Headlight and taillight meshes are separable for emissive materials
- Exhaust tip positions identifiable for VFX anchors

**Technical**
- Polygon count appropriate: the player's car can be 150k–400k triangles; a
  traffic car must be 10k–40k
- Clean UVs, no overlapping shells where it matters
- Materials separated by function: paint, glass, tyre, chrome, plastic, interior
- LODs present, or the mesh is clean enough to decimate

**Legal**
- All seven questions above answered
- No manufacturer badges, or a plan to remove them

An asset that fails the geometry checks is not cheap just because it was free.
Fixing pivots and separating needles for 50 cars is real work.

---

## Where to look

Legitimate sources worth checking, **licence per item**: Unity Asset Store,
Sketchfab, CGTrader, TurboSquid, Poly Haven (CC0 — the safest for HDRIs, textures
and some props), Quixel/Fab, ambientCG (CC0 textures), OpenGameArt.

Poly Haven and ambientCG are CC0 and therefore the least risky places to source
HDRIs, road and concrete textures, and environment props.

# Demo login artwork

All four demo applications embed the same provider-neutral image, `dms-login.png`,
with the logical resource name `CompuMaster.Dms.DemoLoginArtwork.png`. The linked
`DemoLoginArtwork.vb` helper creates an independent form-owned bitmap for each
login. No external image file is needed beside the executable.

An application may supply a provider-specific or branded image when constructing
any demo's `LoginForm`:

```vb
Using artwork As Image = Image.FromFile("my-provider-login.png")
    Using login As New LoginForm(Nothing, artwork)
        login.ShowDialog()
    End Using
End Using
```

Existing constructors keep working. `login.LoginImage = customImage` replaces the
artwork; `login.LoginImage = Nothing` restores the embedded common default.
The setter copies its input, so the caller retains ownership and may dispose the
original immediately. The getter returns the form-owned display image: do not
dispose that borrowed image. Replacing the artwork and disposing the form release
the form-owned copies. Each panel uses `PictureBoxSizeMode.Zoom`, preserving the
aspect ratio of both the default portrait and custom landscape/square pictures.

## Asset provenance

Generated on 2026-10-08 using the built-in Imagegen tool. The output was copied
unchanged into this directory; no downloaded provider logos or ribbon artwork are
included. The illustration contains no text, so it works with every localized
login. This is a presentation change only; authentication and credential handling
retain their existing behavior. RTL GUI layout remains deferred under #66.

Final generation prompt:

> Use case: stylized-concept. Asset type: reusable portrait illustration for the narrow left-hand panel of a professional Windows document-management login dialog. Create a polished, restrained 3D editorial illustration of neatly organized digital documents, a simple open folder, and a small cloud connected by a subtle dotted line, communicating document access and collaboration. Tall portrait 1:2 composition with the complete motif concentrated inside the central 70% of the width and central 65% of the height; generous quiet space above and below; all objects fully visible. Matte porcelain-white documents with subtle page lines and a small folded corner, a muted blue folder, a understated teal accent, soft studio lighting and gentle shadows on a uniform very pale cool-gray background. The illustration must remain recognizable when displayed at about 140 by 265 pixels. Use a single clear grouping and a calm visual hierarchy. No people, no ribbons, no locks dominating the scene, no photographic desk clutter, no text, no letters, no numbers, no brand marks or provider logos, no watermark, no frame, no interface screenshot. Background is opaque, do not use transparency.

Tracked implementation: #78; aggregate PR #75.

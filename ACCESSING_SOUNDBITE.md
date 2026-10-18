# Accessing SoundBite from Windows or macOS

SoundBite is hosted on a Mac and is available to devices connected to the same
Tailscale network (tailnet). It is not a public internet site. Users need Tailscale
access as well as a separate SoundBite account if they want to sign in.

## SoundBite link

Open this link in a browser:

**https://mair.tailae22d2.ts.net/**

Safari, Chrome, Edge, and Firefox should work.

## 1. Join the SoundBite tailnet

1. Ask the SoundBite tailnet administrator to invite you to the tailnet or share
   the Mac running SoundBite with your Tailscale account. Being given the
   SoundBite link alone does not grant network access.
2. On your Windows PC or Mac, go to [tailscale.com/download](https://tailscale.com/download)
   and install the Tailscale app for your operating system.
3. Open Tailscale and sign in using the account or identity authorized by the
   administrator. If the Mac was shared with you, open the Tailscale sharing
   invitation and accept it while signed in to the account you will use on this
   device.
4. Confirm that Tailscale shows as connected and that the Mac named `mair` is
   visible in the Tailscale device list. A connected status without access to
   `mair` is not enough.

The Tailscale account and SoundBite account are separate. Tailscale connects your
device to the private network; the SoundBite account lets you use the app's
signed-in features.

## 2. Open SoundBite

With Tailscale connected, open the link above in your browser. You can bookmark it
for next time. Sign in on SoundBite if you want to rate or review tracks, manage
your account, or use other signed-in features.

## If the link does not open

1. Check that the device is online and Tailscale says **Connected**.
2. Confirm that the Mac `mair` appears in the Tailscale device list. If it does
   not, make sure you accepted the Mac-sharing invitation with the correct
   Tailscale account, or ask the administrator to share the Mac with that
   account.
3. In Windows PowerShell, run:

   ```powershell
   tailscale status
   tailscale ping mair.tailae22d2.ts.net
   Resolve-DnsName mair.tailae22d2.ts.net
   Test-NetConnection mair.tailae22d2.ts.net -Port 443
   ```

   `tailscale status` should list `mair`; the ping and DNS lookup should resolve
   the Mac; and `Test-NetConnection` should report `TcpTestSucceeded : True`.
4. Reopen the exact HTTPS link above. Do not change it to an IP address or an
   `http://` link, and do not bypass a browser security warning.
5. If the checks fail, send the administrator the output of `tailscale status`
   and `Test-NetConnection` (remove any information you do not want to share).
   The host Mac must be powered on, connected to the internet, and running the
   SoundBite service.

SoundBite is served privately through Tailscale; no router port forwarding is
required. Do not share SoundBite or Tailscale passwords in messages.

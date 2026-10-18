# Accessing SoundBite from Windows or macOS

SoundBite is hosted on a Mac and is available to devices connected to the same
Tailscale network (tailnet). It is not a public internet site. Users need Tailscale
access as well as a separate SoundBite account if they want to sign in.

## SoundBite link

Open this link in a browser:

**https://mair.tailae22d2.ts.net/**

Safari, Chrome, Edge, and Firefox should work.

## 1. Join the SoundBite tailnet

1. Ask the SoundBite tailnet administrator to invite you or authorize your device.
   Being given the SoundBite link alone does not grant network access.
2. On your Windows PC or Mac, go to [tailscale.com/download](https://tailscale.com/download)
   and install the Tailscale app for your operating system.
3. Open Tailscale and sign in using the account or identity authorized by the
   administrator. Complete any device approval or sign-in prompts.
4. Confirm that Tailscale shows as connected.

The Tailscale account and SoundBite account are separate. Tailscale connects your
device to the private network; the SoundBite account lets you use the app's
signed-in features.

## 2. Open SoundBite

With Tailscale connected, open the link above in your browser. You can bookmark it
for next time. Sign in on SoundBite if you want to rate or review tracks, manage
your account, or use other signed-in features.

## If the link does not open

1. Check that the device is online and Tailscale says **Connected**.
2. Confirm with the administrator that this device/account has been approved for
   the SoundBite tailnet.
3. Reopen the exact HTTPS link above. Do not change it to an IP address or an
   `http://` link, and do not bypass a browser security warning.
4. If the page still cannot be reached, contact the administrator. The host Mac
   must be powered on, connected to the internet, and running the SoundBite
   service.

SoundBite is served privately through Tailscale; no router port forwarding is
required. Do not share SoundBite or Tailscale passwords in messages.

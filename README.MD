<p align="center">
    <h1 align="center">Google Takeout Timestamps Fixer</h1>
</p>

## Problem

Google Takeout files have timestamps at the time when Takeout were requested.

## Solution

Update all files timestamps according to EXIF data or json metadata provided by Google

## Prerequisites

.Net 8 SDK installed - https://dot.net

## How to use

1. Checkout Repo
2. `dotnet run --dir <Directory with extracted Google Photos>`

## Usage

```markdown
DESCRIPTION:
Fix files timestamps according to EXIF or Google json metadata

USAGE:
    takoutfixer [OPTIONS]

OPTIONS:
    -h, --help         Prints help information
        --dir          Directory with Google Takeout Data
        --dry          Dry run - output affected files list, don't change anything
    -s, --skip-exif    Skip EXIF data, only use json metadata


```
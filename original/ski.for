Copyright 1985 by chris pirih

!	Proverbial Software presents
!		Ski
!		for the VAX/VMS system
!		with ANSI terminals

	parameter
	1	io$_readlblk	='21'x,
	1	io$m_noecho	='40'x,
	1	skor_file	='sys$sysdevice:<pirih.images.user>',
	1	race_length	=1000,
	1	tree_pic	='^'

	character*5 shape/'=/|\='/
	character a,dst*4,tim*11,tim2*11,ob*80,usrnam*12,unam*12,mynam*12,sp*9
	integer ptoff
	integer tree(20)
	integer*2 quad(4),start(7)
	integer*4 starting(2),current(2),entered(2)
	real veryfast/0.2/,fast/0.1/,medium/0.01/,slow/0.005/
	logical outamyway,superfast
	external astwake
	common /logterm/ ltt

c		Define a useful little random number function
	iran(i1)=int(ran(iseed)*i1)+1

c		Protect the image against copying
	call secure('Ski')

	call lib$get_foreign(ob,,i)		! get foreign command line
	outamyway=(ob(1:1) .eq. '>')		! '>' means no trees
	superfast=(ob(1:1) .eq. '.')
	iseed=2*ifix(secnds(0.0)*1000.0)+1	! initialize random numbers
	q=ran(iseed)
	toff=0					! tree mapping offset
	idir=3					! direction skier is facing
	x=40.0					! horizontal co-ordinate
	y=3.0					! vertical co-ordinate
	xv=0.0					! horizontal velocity
	yv=0.0					! vertical velocity
	call lib$erase_page(1,1)		! clear screen
	ltt=iassign()				! set up terminal software
	call passall(ltt)			!  and write out intro
	call lib$put_screen('Proverbial Software Presents',17,2)
	call lib$put_screen('Alpine Skiing and Tree Dodging',18,3)
	call lib$put_screen(' v2.0',,,4)
	call lib$put_screen('--a new sport for VAX/VMS users--',19,4)
	call lib$put_screen('by Chris Pirih',20,10)
	call lib$put_screen('Use 4 & 6 keys to turn skis.',5,2)
	call lib$put_screen('Hit ENTER to start.',6,2)
15	i=inpchr(ltt,-2)			! wait for ENTER
	if (i .eq. 25) goto 32760		! abort if ^Y
	if (i .ne. 13) goto 15
	call set_scroll(1,20)			! set up terminal hardware
	call lib$put_screen(char(27)//'[1v')
	call sys$gettim(starting)		! get starting system time
100	k=inpchr(ltt,-1)			! get a key
110	if (k .eq. 52) idir=idir-1		! 4 -> rotate one way
	if (k .eq. 54) idir=idir+1		! 6 -> rotate other way
	if (k .eq. 25) goto 30000		! ^Y -> quit
	if (idir .gt. 5) idir=5			! check rotation boundaries
	if (idir .lt. 1) idir=1
	if (idir .eq. 1) then			! accelerate skier based on
		call approach(yv,0.0,.03)	!  which direction it's facing
		call approach(xv,0.0,slow)
	else if (idir .eq. 2) then
		call approach(yv,0.3,medium+medium*yv)
		call approach(xv,-0.3,veryfast*yv)
	else if (idir .eq. 3) then
		call approach(yv,1.0,medium)
		call approach(xv,0.0,fast)
	else if (idir .eq. 4) then
		call approach(yv,0.3,medium+medium*yv)
		call approach(xv,0.3,veryfast*yv)
	else if (idir .eq. 5) then
		call approach(yv,0.0,.03)
		call approach(xv,0.0,slow)
	end if
	toff=toff+yv				! move up the trees
	if (toff .gt. 20.0) toff=0.0		! (it's a circular queue)
	x=x+xv					! move over skier
	if (x .gt. 78.0) x=78.0			! check for edges of slope
	if (x .lt. 1.0) x=1.0
	a=shape(idir:idir)			! figure out skier's shape
	if ((int(x) .ne. ix) .or. (int(toff) .ne. ptoff)) then
		call lib$put_screen('  ',int(y),ix)	! erase if necessary
	end if
	if (int(toff) .ne. ptoff) then		! move trees if necessary
		i=20+int(toff)
		if (i .gt. 20) i=i-20
		if (outamyway) then		! generate tree
			tree(i)=79		! (no trees, please!)
		else if (superfast) then
1091			tree(i)=iran(80)
			if ((tree(i) .eq. int(x)) .and. 
	1			(iran(2) .eq. 1)) goto 1091
		else
			j=(iran(iran(80))-1)*(iran(2)*2-3)+int(x)
			if (j .gt. 80) j=j-80	! create a random tree
			if (j .lt. 1) j=j+80
			tree(i)=j
		end if
		call lib$put_screen(char(10)//tree_pic,20,tree(i)) ! draw tree
		jc=jc+1				! increment meter subcount
		if (jc .eq. 8) then		! room for next meter marker...
			meter=meter+50
			call ots$cvt_l_ti(meter,dst,%val(2))
			call lib$put_screen(char(13)//dst)
		else if (jc .eq. 25) then	! passing meter marker...
			call sys$gettim(current)	! write current time
			call subq(starting,current)
			call sys$asctim(,tim,current,%val(1))
			call lib$put_screen(tim(4:11),22,1)
			if (meter .eq. race_length) goto 32767	! end of race!
			jc=0
		end if
		ptoff=int(toff)			! save previous tree offset
	end if
	ix=int(x)				! locate skier on screen
	iy=int(y)
	call lib$put_screen(a//a,iy,ix)		! draw 'em there
	i=3+ptoff				! which tree is closest?
	if (i .gt. 20) i=i-20
	if ((tree(i) .eq. ix) .or. (tree(i) .eq. ix+1)) then
		call lib$put_screen('  ',iy,ix)		! ran into it!
		do j=0,2			! BOOM!
			call lib$put_screen(shape(3-j:3-j),iy,ix-j-1)
			call lib$put_screen(shape(3+j:3+j),iy,ix+j+1)
			call delay
			call lib$put_screen(' ',iy,ix-j-1)
			call lib$put_screen(' ',iy,ix+j+1)
		end do
		call sys$bintim('0 ::'//char(int(yv*3)+48)//'.2',quad)
		call sys$schdwk(,,quad,)
		call sys$hiber()		! it takes time to get up...
		tree(i)=0		! smashed the hell out of that tree!
		xv=0.0				! stop moving
		yv=0.0
		idir=3				! face downhill again
		mash=mash+1			! increment crash count
1330		i=inpchr(ltt,-1)		! purge typeahead
		if (i .eq. 25) goto 30000	! abort if ^Y
		if (i .ne. 0) goto 1330
		goto 100			! loop!
	end if
	if (yv .ne. 0) goto 100			! still moving? aye, loop
	if (xv .ne. 0) goto 100			!		aye, loop
	call sys$qio(,%val(ltt),%val(io$_readlblk .or. io$m_noecho),,
	1	astwake,,%ref(k),%val(1),,,,)
	call sys$hiber()			! wait for some input
	goto 110				! then loop

c		Abort (^Y) handler
30000	call set_scroll(1,23)			! reset terminal hardware
	call lib$erase_page(1,1)
	call lib$put_screen(char(27)//'[;3;5v',23,1)
	goto 32760				! go exit

c		Handle end of race
32767	call lib$put_screen(a//a,int(y),int(x),4)	! flash skier
	call set_scroll(1,23)			! reset terminal hardware
	call lib$put_screen(char(27)//'[;3;5v',22,1)
	call fred				! wait for a while
32754	if (inpchr(ltt,-1) .ne. 0) goto 32754	! purge keyboard buffer
	s=float(race_length)/1609.0		! race length in miles...
	h=fquad(current)/36e9			! race time in hours...
	mph=ifix(s/h)				! average miles/hour
	call sys$fao('Your average speed was !SL mile!%S per hour.',
	1	l,ob,%val(mph))
	call lib$put_output(ob(1:l))
	call sys$fao('You crashed !SL time!%S.',l,ob,%val(mash))
	call lib$put_output(ob(1:l)//char(10))

c		Update proper high score list, if necessary
3001	format (' ',i2,'      ',a12,'     ',a8,'    ',a11)
	call speed(sp)				! calculate terminal speed
	open (unit=1,file=skor_file//sp//'.ski',status='old',shared,
	1	access='direct',organization='relative',
	1	recordtype='fixed',recl=28,maxrec=10)
	if (outamyway) goto 3050		! skip update if no trees!
	myloc=0					! prepare for search
	myprev=10
	mynam=usrnam()
	do i=1,10				! look for proper place
		read (1,rec=i) unam,quad,entered
		if ((iqg(quad,current) .or. iqz(quad))
	1		.and. (myloc .eq. 0)) myloc=i
		if (unam .eq. mynam) myprev=i
	end do
	if ((myloc .eq. 0) .or. (myloc .gt. myprev)) goto 3050
	do j=myprev,myloc+1,-1			! make room in list
		read (1,rec=j-1) unam,quad,entered
		write (1,rec=j) unam,quad,entered
	end do
	call sys$gettim(entered)
	write (1,rec=myloc) mynam,current,entered  ! add this user
3050	print *, 'Rank    Name             Time        When'
	i=1
	do while (i .le. 10)
		read (1,rec=i) unam,quad,entered
		if (iqz(quad)) then
			i=11
		else
			call sys$asctim(,tim,quad,%val(1))
			call sys$asctim(,tim2,entered,)
			write (6,3001) i,unam,tim(4:11),tim2(1:11)
			i=i+1
		end if
	end do
	close (1)
	print *,' '				! one blank line, please

32760	call lib$put_screen(char(10))		! one more blank line, please
	call nopassall(ltt)			! fix terminal software
	end					! done!!!!!

c		Set a scrolling region (I don't trust lib$set_scroll)
	subroutine set_scroll(itop,ibottom)
	character*2 a,b
	call ots$cvt_l_ti(itop,a,%val(2))
	call ots$cvt_l_ti(ibottom,b,%val(2))
	call lib$put_screen(char(27)//'['//a//';'//b//'r')
	return
	end

c		Mod function: i=(i mod j)
	subroutine mod(i,j)
	do while (i .ge. j)
		i=i-j
	end do
	return
	end

c		Accelerate X toward XLIM, at speed SPEED
	subroutine approach(x,xlim,speed)
	y=sgn(xlim-x)
	x=x+y*speed
	if (y .ne. sgn(xlim-x)) x=xlim
	return
	end

c		Floating point sign
	function sgn(x)
	if (x .lt. 0.0) then
		sgn=-1.0
	else
		sgn=1.0
	end if
	return
	end

c		Wait for a very short time
	subroutine delay
	integer*2 quad(4)
	call sys$bintim('0 ::0.05',quad)
	call sys$schdwk(,,quad,)
	call sys$hiber()
	return
	end

c		Convert quadword to longword
	function long(q)
	parameter maxint=2147483647
	integer*4 q(2)
	if (q(2) .eq. 0) then
		long=q(1)
	else if (q(2) .lt. 0) then
		long=-maxint-1
	else
		long=maxint
	end if
	return
	end

c		Convert quadword to floating
	function fquad(q)
	integer*4 q(2)
	xlong=2.0**32
	f1=q(1)
	if (f1 .lt. 0) f1=f+xlong
	f2=q(2)
	if (f2 .lt. 0) f2=f2+xlong
	fquad=f1+f2*xlong
	return
	end

c		Calcualate terminal speed
	subroutine speed(na)
	parameter
	1	io$_sensemode	= '27'x,
	1	tt$c_baud_2400	= '0B'x,
	1	tt$c_baud_4800	= '0D'x,
	1	tt$c_baud_9600	= '0F'x
	character*(*) na
	logical*1 stats(8),modes(8)
	common /logterm/ ltt
	call sys$qiow(,%val(ltt),%val(io$_sensemode),stats,,,modes,%val(8),,,,)
	i=stats(3)
	if (i .gt. tt$c_baud_9600) then
		na='very fast'
	else if (i .gt. tt$c_baud_4800) then
		na='fast'
	else if (i .gt. tt$c_baud_2400) then
		na='4800 baud'
	else
		na='slow'
	end if
	return
	end

c		Another name for SLOW
	subroutine fred
	call slow
	return
	end
 
